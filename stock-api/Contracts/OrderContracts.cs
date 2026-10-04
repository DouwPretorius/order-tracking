namespace stock_api.Contracts;

public sealed record OrderRequest(int CustomerId, string Status, List<OrderLineItemRequest> Items);

public sealed record OrderLineItemRequest(string Name, string? Sku, decimal Quantity, decimal UnitPrice);

public sealed record OrderLineItemResponse(int Id, string Name, string? Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderResponse(
    int Id,
    int? CustomerId,
    string CustomerName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<OrderLineItemResponse> Items,
    decimal Total);
