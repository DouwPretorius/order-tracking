using stock_api.Contracts;
using stock_api.Services;
using Xunit;

namespace stock_api.Tests;

public sealed class OrderRulesTests
{
    [Theory]
    [InlineData("Pending", "Processing", true)]
    [InlineData("Pending", "Cancelled", true)]
    [InlineData("Processing", "Confirmed", true)]
    [InlineData("Processing", "Cancelled", true)]
    [InlineData("Confirmed", "Completed", true)]
    [InlineData("Confirmed", "Cancelled", true)]
    [InlineData("Completed", "Pending", false)]
    [InlineData("Cancelled", "Pending", false)]
    [InlineData("Pending", "Completed", false)]
    public void CanMoveTo_only_allows_expected_status_transitions(
        string currentStatus,
        string nextStatus,
        bool expected)
    {
        Assert.Equal(expected, OrderRules.CanMoveTo(currentStatus, nextStatus));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ValidateItems_rejects_non_positive_quantities(decimal quantity)
    {
        var items = new[] { new OrderLineItemRequest("Test item", null, quantity, 1m) };

        var error = OrderRules.ValidateItems(items);

        Assert.Equal(
            "Each item quantity must be a positive whole number no greater than 2,147,483,647.",
            error);
    }

    [Fact]
    public void ValidateItems_accepts_a_positive_whole_quantity()
    {
        var items = new[] { new OrderLineItemRequest("Test item", null, 1m, 1m) };

        Assert.Null(OrderRules.ValidateItems(items));
    }

    [Fact]
    public void DuplicateSubmissionPolicy_blocks_submissions_inside_the_window()
    {
        var now = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);

        Assert.True(DuplicateSubmissionPolicy.IsWithinWindow(now.AddSeconds(-9), now, 10));
        Assert.True(DuplicateSubmissionPolicy.IsWithinWindow(now.AddSeconds(-10), now, 10));
        Assert.False(DuplicateSubmissionPolicy.IsWithinWindow(now.AddSeconds(-11), now, 10));
    }

    [Fact]
    public void DuplicateSubmissionPolicy_allows_all_submissions_when_disabled()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(DuplicateSubmissionPolicy.IsWithinWindow(now, now, 0));
    }
}
