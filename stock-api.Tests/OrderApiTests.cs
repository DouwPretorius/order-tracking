using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using stock_api.Contracts;
using Xunit;

namespace stock_api.Tests;

public sealed class OrderApiTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Customer_endpoints_support_create_read_update_and_delete()
    {
        var createResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "  Customer CRUD Test  ",
            email = "crud@example.com",
            phone = "021-555-0100",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Customer CRUD Test", created.Name);

        var list = await client.GetFromJsonAsync<List<CustomerResponse>>("/api/customers", JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list, customer => customer.Id == created.Id && customer.Email == "crud@example.com");

        var updateResponse = await client.PutAsJsonAsync($"/api/customers/{created.Id}", new
        {
            name = "Updated CRUD Customer",
            email = "updated@example.com",
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated CRUD Customer", updated.Name);
        Assert.Equal("updated@example.com", updated.Email);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/customers/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/customers/{created.Id}")).StatusCode);
        var afterDelete = await client.GetFromJsonAsync<List<CustomerResponse>>("/api/customers", JsonOptions);
        Assert.NotNull(afterDelete);
        Assert.DoesNotContain(afterDelete, customer => customer.Id == created.Id);
    }

    [Fact]
    public async Task Customer_creation_rejects_phone_characters_other_than_numbers_and_hyphens()
    {
        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Invalid Phone Customer",
            email = (string?)null,
            phone = "021 555 ABC",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("Phone can contain numbers and hyphens only.", error.Message);
    }

    [Fact]
    public async Task Customer_creation_rejects_duplicate_email_addresses_without_case_sensitivity()
    {
        var firstResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "First Email Customer",
            email = "unique-email@example.com",
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Duplicate Email Customer",
            email = "  UNIQUE-EMAIL@example.com  ",
            phone = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var error = await duplicateResponse.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("A customer with this email address already exists.", error.Message);

        var secondCustomerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Second Email Customer",
            email = "second-email@example.com",
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, secondCustomerResponse.StatusCode);
        var secondCustomer = await secondCustomerResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(secondCustomer);

        var duplicateUpdateResponse = await client.PutAsJsonAsync($"/api/customers/{secondCustomer.Id}", new
        {
            name = "Second Email Customer",
            email = "unique-email@example.com",
            phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateUpdateResponse.StatusCode);
        var duplicateUpdateError =
            await duplicateUpdateResponse.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        Assert.NotNull(duplicateUpdateError);
        Assert.Equal("A customer with this email address already exists.", duplicateUpdateError.Message);
    }

    [Fact]
    public async Task Order_endpoints_support_create_read_update_and_delete()
    {
        var customerId = await CreateCustomerAsync("Order CRUD Test");
        var createResponse = await client.PostAsJsonAsync("/api/orders", OrderRequest(
            customerId, "Pending", "Catan: Seafarers", 2, 34.99m));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Pending", created.Status);
        Assert.Equal(69.98m, created.Total);
        Assert.Equal(69.98m, Assert.Single(created.Items).LineTotal);

        var duplicateResponse = await client.PostAsJsonAsync("/api/orders", OrderRequest(
            customerId, "Pending", "Catan: Seafarers", 2, 34.99m));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var duplicateError = await duplicateResponse.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        Assert.NotNull(duplicateError);
        Assert.Contains($"order #{created.Id}", duplicateError.Message);
        Assert.Contains("No new order was created.", duplicateError.Message);

        var getResponse = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{created.Id}", JsonOptions);
        Assert.NotNull(getResponse);
        Assert.Equal("Catan: Seafarers", Assert.Single(getResponse.Items).Name);

        var orderList = await client.GetFromJsonAsync<List<OrderResponse>>("/api/orders", JsonOptions);
        Assert.NotNull(orderList);
        Assert.Contains(orderList, order => order.Id == created.Id);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/orders/{created.Id}",
            OrderRequest(customerId, "Processing", "Wingspan", 3, 20m));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Processing", updated.Status);
        Assert.Equal(60m, updated.Total);
        Assert.Equal("Wingspan", Assert.Single(updated.Items).Name);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/orders/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Order_creation_rejects_fractional_quantities()
    {
        var customerId = await CreateCustomerAsync("Fractional Quantity Test");
        var response = await client.PostAsJsonAsync(
            "/api/orders",
            OrderRequest(customerId, "Pending", "Invalid quantity item", 1.5m, 10m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Contains("positive whole number", error.Message);
    }

    [Fact]
    public async Task Status_transitions_are_enforced_by_the_api()
    {
        var customerId = await CreateCustomerAsync("Status Flow Test");
        var order = await CreateOrderAsync(customerId, "Status Flow Test A");

        var nonPendingCreate = await client.PostAsJsonAsync(
            "/api/orders",
            OrderRequest(customerId, "Processing", "Must start Pending", 1, 1m));
        Assert.Equal(HttpStatusCode.BadRequest, nonPendingCreate.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await UpdateOrderAsync(order, customerId, "Confirmed")).StatusCode);
        Assert.Equal("Pending", (await GetOrderAsync(order.Id)).Status);

        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(order, customerId, "Processing")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateOrderAsync(order, customerId, "Completed")).StatusCode);
        Assert.Equal("Processing", (await GetOrderAsync(order.Id)).Status);

        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(order, customerId, "Confirmed")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateOrderAsync(order, customerId, "Pending")).StatusCode);
        Assert.Equal("Confirmed", (await GetOrderAsync(order.Id)).Status);

        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(order, customerId, "Completed")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateOrderAsync(order, customerId, "Cancelled")).StatusCode);
        Assert.Equal("Completed", (await GetOrderAsync(order.Id)).Status);

        var pendingCancellation = await CreateOrderAsync(customerId, "Pending Cancellation Test");
        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(pendingCancellation, customerId, "Cancelled")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateOrderAsync(pendingCancellation, customerId, "Processing")).StatusCode);
        Assert.Equal("Cancelled", (await GetOrderAsync(pendingCancellation.Id)).Status);

        var processingCancellation = await CreateOrderAsync(customerId, "Processing Cancellation Test");
        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(processingCancellation, customerId, "Processing")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(processingCancellation, customerId, "Cancelled")).StatusCode);

        var confirmedCancellation = await CreateOrderAsync(customerId, "Confirmed Cancellation Test");
        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(confirmedCancellation, customerId, "Processing")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(confirmedCancellation, customerId, "Confirmed")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UpdateOrderAsync(confirmedCancellation, customerId, "Cancelled")).StatusCode);
    }

    private async Task<int> CreateCustomerAsync(string name)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, email = (string?)null, phone = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(customer);
        return customer.Id;
    }

    private async Task<OrderResponse> CreateOrderAsync(int customerId, string itemName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/orders",
            OrderRequest(customerId, "Pending", itemName, 1, 1m));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(order);
        return order;
    }

    private async Task<HttpResponseMessage> UpdateOrderAsync(OrderResponse order, int customerId, string status) =>
        await client.PutAsJsonAsync(
            $"/api/orders/{order.Id}",
            OrderRequest(customerId, status, order.Items[0].Name, 1, 1m));

    private async Task<OrderResponse> GetOrderAsync(int orderId)
    {
        var order = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}", JsonOptions);
        Assert.NotNull(order);
        return order;
    }

    private static object OrderRequest(int customerId, string status, string itemName, decimal quantity, decimal unitPrice) =>
        new
        {
            customerId,
            status,
            items = new[] { new { name = itemName, sku = "", quantity, unitPrice } },
        };

}
