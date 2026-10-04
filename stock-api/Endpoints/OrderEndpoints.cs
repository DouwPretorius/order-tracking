using Microsoft.EntityFrameworkCore;
using stock_api.Contracts;
using stock_api.Data;
using stock_api.Models;
using stock_api.Services;

namespace stock_api.Endpoints;

public static class OrderEndpoints
{
    /// <summary>Registers the order CRUD routes under <c>/api/orders</c>.</summary>
    /// <param name="endpoints">The route builder that receives the order routes.</param>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var orders = endpoints.MapGroup("/api/orders");
        orders.MapGet("", GetOrders);
        orders.MapGet("/{id:int}", GetOrder);
        orders.MapPost("", (OrderRequest request, OrderSubmissionService service) =>
            service.CreateOrderAsync(request));
        orders.MapPut("/{id:int}", UpdateOrder);
        orders.MapDelete("/{id:int}", DeleteOrder);
    }

    /// <summary>Returns orders with their customer and line-item details, newest first.</summary>
    /// <param name="db">The database context used to read order records.</param>
    private static async Task<IResult> GetOrders(OrderDbContext db)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
        return Results.Ok(orders.Select(OrderSubmissionService.ToOrderResponse));
    }

    /// <summary>Returns one order with its customer and line-item details.</summary>
    /// <param name="id">The positive integer identifier from the order route.</param>
    /// <param name="db">The database context used to find the order.</param>
    private static async Task<IResult> GetOrder(int id, OrderDbContext db)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(entry => entry.Customer)
            .Include(entry => entry.Items)
            .FirstOrDefaultAsync(entry => entry.Id == id);
        return order is null
            ? Results.NotFound(new ApiErrorResponse("Order not found."))
            : Results.Ok(OrderSubmissionService.ToOrderResponse(order));
    }

    /// <summary>Validates and updates an order when its requested status transition is allowed.</summary>
    /// <param name="id">The positive integer identifier from the order route.</param>
    /// <param name="request">The requested customer, status, and line items for the updated order.</param>
    /// <param name="db">The database context used to load and persist the order.</param>
    /// <param name="submissionService">The shared order validation and mapping service.</param>
    private static async Task<IResult> UpdateOrder(
        int id,
        OrderRequest request,
        OrderDbContext db,
        OrderSubmissionService submissionService)
    {
        var validationError = await submissionService.ValidateOrderAsync(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new ApiErrorResponse(validationError));
        }

        var order = await db.Orders
            .Include(entry => entry.Customer)
            .Include(entry => entry.Items)
            .FirstOrDefaultAsync(entry => entry.Id == id);
        if (order is null)
        {
            return Results.NotFound(new ApiErrorResponse("Order not found."));
        }
        if (!OrderRules.CanMoveTo(order.Status, request.Status))
        {
            return Results.Conflict(new ApiErrorResponse(OrderRules.TransitionErrorMessage(order.Status)));
        }

        order.CustomerId = request.CustomerId;
        order.CustomerNameSnapshot = await db.Customers
            .Where(customer => customer.Id == request.CustomerId)
            .Select(customer => customer.Name)
            .SingleAsync();
        order.Status = request.Status;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        order.Total = OrderSubmissionService.CalculateOrderTotal(request);
        db.OrderLineItems.RemoveRange(order.Items);
        order.Items = request.Items.Select(OrderSubmissionService.ToLineItem).ToList();
        await db.SaveChangesAsync();
        await db.Entry(order).Reference(entry => entry.Customer).LoadAsync();
        return Results.Ok(OrderSubmissionService.ToOrderResponse(order));
    }

    /// <summary>Deletes an order and its dependent line items.</summary>
    /// <param name="id">The positive integer identifier from the order route.</param>
    /// <param name="db">The database context used to find and delete the order.</param>
    private static async Task<IResult> DeleteOrder(int id, OrderDbContext db)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null)
        {
            return Results.NotFound(new ApiErrorResponse("Order not found."));
        }

        db.Orders.Remove(order);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
