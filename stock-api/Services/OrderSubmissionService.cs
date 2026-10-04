using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using stock_api.Contracts;
using stock_api.Data;
using stock_api.Models;

namespace stock_api.Services;

public sealed class OrderSubmissionService(
    OrderDbContext db,
    IOptions<OrderSubmissionOptions> options)
{
    private readonly int duplicateWindowSeconds = options.Value.DuplicateWindowSeconds;

    /// <summary>Validates and creates an order, rejecting an identical recent submission.</summary>
    /// <param name="request">The customer, initial status, and line items supplied for the new order.</param>
    /// <returns>An HTTP result containing the created order or an appropriate validation/conflict response.</returns>
    public async Task<IResult> CreateOrderAsync(OrderRequest request)
    {
        var validationError = await ValidateOrderAsync(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new ApiErrorResponse(validationError));
        }
        if (request.Status != "Pending")
        {
            return Results.BadRequest(new ApiErrorResponse("New orders must start in Pending status."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var duplicateKey = duplicateWindowSeconds == 0 ? null : CreateDuplicateKey(request);
        if (duplicateKey is not null)
        {
            var lockKey = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(duplicateKey));
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})");

            var duplicate = await db.Orders
                .AsNoTracking()
                .Where(order => order.DuplicateKey == duplicateKey)
                .OrderByDescending(order => order.CreatedAt)
                .FirstOrDefaultAsync();
            if (duplicate is not null &&
                DuplicateSubmissionPolicy.IsWithinWindow(
                    duplicate.CreatedAt,
                    DateTimeOffset.UtcNow,
                    duplicateWindowSeconds))
            {
                return Results.Conflict(new ApiErrorResponse(
                    $"An identical order was just submitted as order #{duplicate.Id}. No new order was created."));
            }
        }

        var order = new Order
        {
            CustomerId = request.CustomerId,
            CustomerNameSnapshot = await db.Customers
                .Where(customer => customer.Id == request.CustomerId)
                .Select(customer => customer.Name)
                .SingleAsync(),
            Status = request.Status,
            DuplicateKey = duplicateKey,
            Total = CalculateOrderTotal(request),
            Items = request.Items.Select(ToLineItem).ToList(),
        };
        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_OrderLineItems_OrderId_NormalizedSku",
            })
        {
            return Results.Conflict(new ApiErrorResponse(
                "Each non-empty SKU can only appear once in an order. Check the line items and try again."));
        }
        await db.Entry(order).Reference(entry => entry.Customer).LoadAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/orders/{order.Id}", ToOrderResponse(order));
    }

    /// <summary>Checks that the customer exists and that the requested status and line items are valid.</summary>
    /// <param name="request">The customer, status, and line items to validate.</param>
    /// <returns>A validation message, or <see langword="null"/> when the request is valid.</returns>
    public async Task<string?> ValidateOrderAsync(OrderRequest request)
    {
        if (request.CustomerId <= 0 || !await db.Customers.AnyAsync(customer => customer.Id == request.CustomerId))
        {
            return "Select an existing customer.";
        }
        if (!OrderRules.IsValidStatus(request.Status))
        {
            return "Choose a valid order status.";
        }

        return OrderRules.ValidateItems(request.Items);
    }

    /// <summary>Converts a validated line-item request into a persistable entity.</summary>
    /// <param name="item">The line-item name, optional SKU, positive whole quantity, and unit price.</param>
    internal static OrderLineItem ToLineItem(OrderLineItemRequest item) =>
        new()
        {
            Name = item.Name.Trim(),
            Sku = CleanOptional(item.Sku),
            Quantity = (int)item.Quantity,
            UnitPrice = item.UnitPrice,
            LineTotal = decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero),
        };

    /// <summary>Calculates an order total from the rounded line-item totals.</summary>
    /// <param name="request">The validated order request containing its line items.</param>
    internal static decimal CalculateOrderTotal(OrderRequest request) =>
        request.Items.Sum(item => decimal.Round(
            item.Quantity * item.UnitPrice,
            2,
            MidpointRounding.AwayFromZero));

    /// <summary>Maps a stored order and its line items to the public API response shape.</summary>
    /// <param name="order">The order entity to expose.</param>
    internal static OrderResponse ToOrderResponse(Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.CustomerNameSnapshot,
            order.Status,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(item => new OrderLineItemResponse(
                item.Id,
                item.Name,
                item.Sku,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal)).ToList(),
            order.Total);

    /// <summary>Creates a stable hash for requests with the same customer and equivalent line items.</summary>
    /// <param name="request">A validated order request whose line items are canonicalized before hashing.</param>
    private static string CreateDuplicateKey(OrderRequest request)
    {
        var items = request.Items
            .Select(item => new DuplicateOrderItem(
                item.Name.Trim(),
                CleanOptional(item.Sku) ?? string.Empty,
                (int)item.Quantity,
                item.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Sku, StringComparer.Ordinal)
            .ThenBy(item => item.Quantity)
            .ThenBy(item => item.UnitPrice, StringComparer.Ordinal)
            .ToArray();
        var canonicalOrder = JsonSerializer.SerializeToUtf8Bytes(new DuplicateOrder(request.CustomerId, items));
        return Convert.ToHexString(SHA256.HashData(canonicalOrder));
    }

    /// <summary>Trims an optional string and converts empty or whitespace-only values to <see langword="null"/>.</summary>
    /// <param name="value">The optional string to normalize.</param>
    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DuplicateOrder(int CustomerId, DuplicateOrderItem[] Items);

    private sealed record DuplicateOrderItem(string Name, string Sku, int Quantity, string UnitPrice);
}
