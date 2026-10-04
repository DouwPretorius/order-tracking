namespace stock_api.Models;

public sealed class OrderLineItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public required string Name { get; set; }
    public string? Sku { get; set; }
    public string? NormalizedSku { get; private set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
