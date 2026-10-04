using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using stock_api.Contracts;
using stock_api.Data;
using stock_api.Models;

namespace stock_api.Endpoints;

public static class CustomerEndpoints
{
    /// <summary>Registers the customer CRUD routes under <c>/api/customers</c>.</summary>
    /// <param name="endpoints">The route builder that receives the customer routes.</param>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var customers = endpoints.MapGroup("/api/customers");
        customers.MapGet("", GetCustomers);
        customers.MapPost("", CreateCustomer);
        customers.MapPut("/{id:int}", UpdateCustomer);
        customers.MapDelete("/{id:int}", DeleteCustomer);
    }

    /// <summary>Returns customers ordered by name.</summary>
    /// <param name="db">The database context used to read customer records.</param>
    private static async Task<IResult> GetCustomers(OrderDbContext db)
    {
        var customers = await db.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ToListAsync();
        return Results.Ok(customers.Select(ToCustomerResponse));
    }

    /// <summary>Validates and creates a customer, rejecting an email already in use.</summary>
    /// <param name="request">The customer name and optional contact details to create.</param>
    /// <param name="db">The database context used to persist the customer.</param>
    private static async Task<IResult> CreateCustomer(CustomerRequest request, OrderDbContext db)
    {
        var validationError = ValidateCustomer(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new ApiErrorResponse(validationError));
        }

        var email = CleanOptional(request.Email);
        if (await EmailAlreadyExists(db, email))
        {
            return DuplicateEmailResponse();
        }

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Email = email,
            Phone = CleanOptional(request.Phone),
        };
        db.Customers.Add(customer);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsDuplicateEmailViolation(exception))
        {
            return DuplicateEmailResponse();
        }
        return Results.Created($"/api/customers/{customer.Id}", ToCustomerResponse(customer));
    }

    /// <summary>Validates and applies updated customer details.</summary>
    /// <param name="id">The positive integer identifier from the customer route.</param>
    /// <param name="request">The replacement customer name and optional contact details.</param>
    /// <param name="db">The database context used to find and update the customer.</param>
    private static async Task<IResult> UpdateCustomer(int id, CustomerRequest request, OrderDbContext db)
    {
        var validationError = ValidateCustomer(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new ApiErrorResponse(validationError));
        }

        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
        {
            return Results.NotFound(new ApiErrorResponse("Customer not found."));
        }

        var email = CleanOptional(request.Email);
        if (await EmailAlreadyExists(db, email, id))
        {
            return DuplicateEmailResponse();
        }

        customer.Name = request.Name.Trim();
        customer.Email = email;
        customer.Phone = CleanOptional(request.Phone);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsDuplicateEmailViolation(exception))
        {
            return DuplicateEmailResponse();
        }
        return Results.Ok(ToCustomerResponse(customer));
    }

    /// <summary>Deletes a customer when no linked order is incomplete.</summary>
    /// <param name="id">The positive integer identifier from the customer route.</param>
    /// <param name="db">The database context used to check and delete the customer.</param>
    private static async Task<IResult> DeleteCustomer(int id, OrderDbContext db)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
        {
            return Results.NotFound(new ApiErrorResponse("Customer not found."));
        }

        if (await db.Orders.AnyAsync(order => order.CustomerId == id && order.Status != "Completed"))
        {
            return Results.Conflict(new ApiErrorResponse(
                "This customer has orders that are not completed. Complete or remove those orders before deleting the customer."));
        }

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>Checks the required name and optional email and phone values in a customer request.</summary>
    /// <param name="request">The customer data supplied by the caller.</param>
    /// <returns>A validation message, or <see langword="null"/> when the request is valid.</returns>
    private static string? ValidateCustomer(CustomerRequest request)
    {
        var email = CleanOptional(request.Email);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Customer name is required.";
        }
        if (request.Name.Trim().Length > 120)
        {
            return "Customer name must be 120 characters or fewer.";
        }
        if (email?.Length > 254)
        {
            return "Email must be 254 characters or fewer.";
        }
        if (request.Phone?.Length > 40)
        {
            return "Phone must be 40 characters or fewer.";
        }
        if (!string.IsNullOrWhiteSpace(request.Phone) &&
            request.Phone.Any(character => !char.IsAsciiDigit(character) && character != '-'))
        {
            return "Phone can contain numbers and hyphens only.";
        }
        if (email is not null && !new EmailAddressAttribute().IsValid(email))
        {
            return "Enter a valid email address.";
        }

        return null;
    }

    /// <summary>Trims an optional string and converts empty or whitespace-only values to <see langword="null"/>.</summary>
    /// <param name="value">The optional string to normalize.</param>
    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Task<bool> EmailAlreadyExists(OrderDbContext db, string? email, int? excludingCustomerId = null)
    {
        if (email is null)
        {
            return Task.FromResult(false);
        }

        var normalizedEmail = email.ToLowerInvariant();
        return db.Customers.AnyAsync(customer =>
            customer.Id != excludingCustomerId &&
            EF.Property<string?>(customer, "NormalizedEmail") == normalizedEmail);
    }

    private static IResult DuplicateEmailResponse() =>
        Results.Conflict(new ApiErrorResponse("A customer with this email address already exists."));

    private static bool IsDuplicateEmailViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Customers_NormalizedEmail",
        };

    /// <summary>Maps a stored customer to the public API response shape.</summary>
    /// <param name="customer">The customer entity to expose.</param>
    private static CustomerResponse ToCustomerResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt);
}
