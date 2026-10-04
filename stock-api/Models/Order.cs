namespace stock_api.Models;

public sealed class Order
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public required string CustomerNameSnapshot { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DuplicateKey { get; set; }
    public decimal Total { get; set; }
    public List<OrderLineItem> Items { get; set; } = [];
}
