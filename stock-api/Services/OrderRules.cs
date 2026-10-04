using stock_api.Contracts;

namespace stock_api.Services;

public static class OrderRules
{
    private static readonly string[] Statuses = ["Pending", "Processing", "Confirmed", "Completed", "Cancelled"];

    private static readonly IReadOnlyDictionary<string, string[]> Transitions = new Dictionary<string, string[]>
    {
        ["Pending"] = ["Processing", "Cancelled"],
        ["Processing"] = ["Confirmed", "Cancelled"],
        ["Confirmed"] = ["Completed", "Cancelled"],
        ["Completed"] = [],
        ["Cancelled"] = [],
    };

    /// <summary>Checks whether a status value is one of the supported order statuses.</summary>
    /// <param name="status">The status string supplied for an order.</param>
    public static bool IsValidStatus(string status) => Statuses.Contains(status);

    /// <summary>Checks whether an order may move from its current status to a requested status.</summary>
    /// <param name="currentStatus">The order's current status.</param>
    /// <param name="nextStatus">The requested status.</param>
    public static bool CanMoveTo(string currentStatus, string nextStatus) =>
        currentStatus == nextStatus ||
        Transitions.TryGetValue(currentStatus, out var allowedStatuses) && allowedStatuses.Contains(nextStatus);

    /// <summary>Builds the validation message describing which transitions are allowed from a status.</summary>
    /// <param name="status">The order's current status.</param>
    public static string TransitionErrorMessage(string status) =>
        !Transitions.TryGetValue(status, out var allowedStatuses) || allowedStatuses.Length == 0
            ? $"An order in {status} status cannot move to another status."
            : $"An order in {status} status can only move to {string.Join(" or ", allowedStatuses)}.";

    /// <summary>Validates a collection of order line items, including quantity, SKU, name, and price constraints.</summary>
    /// <param name="items">The line items to validate; must contain at least one item.</param>
    /// <returns>A validation message for the first invalid condition, or <see langword="null"/> when valid.</returns>
    public static string? ValidateItems(IReadOnlyList<OrderLineItemRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            return "An order must contain at least one line item.";
        }
        if (items.Any(item => item is null))
        {
            return "Every order line item must be provided.";
        }
        if (items.Any(item =>
            item.Quantity <= 0 ||
            item.Quantity != decimal.Truncate(item.Quantity) ||
            item.Quantity > int.MaxValue))
        {
            return "Each item quantity must be a positive whole number no greater than 2,147,483,647.";
        }

        var duplicateSku = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .GroupBy(item => item.Sku!.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSku is not null)
        {
            return $"SKU '{duplicateSku.Key}' can only appear once in an order.";
        }
        if (items.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
            item.Name.Trim().Length > 160 || item.Sku?.Trim().Length > 64 ||
            item.UnitPrice < 0 || item.UnitPrice != decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero) ||
            item.UnitPrice > 9_999_999_999.99m ||
            item.UnitPrice * item.Quantity > 9_999_999_999.99m))
        {
            return "Each line item needs a name (up to 160 characters), an optional SKU (up to 64 characters), a positive quantity, and a unit price with at most two decimals. The line total must not exceed R9,999,999,999.99.";
        }
        if (items.Sum(item =>
                decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero)) > 9_999_999_999.99m)
        {
            return "The order total must not exceed R9,999,999,999.99.";
        }

        return null;
    }
}
