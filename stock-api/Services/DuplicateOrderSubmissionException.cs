namespace stock_api.Services;

public sealed class DuplicateOrderSubmissionException(int originalOrderId)
    : Exception($"An identical order was just submitted as order #{originalOrderId}. No new order was created.")
{
    public int OriginalOrderId { get; } = originalOrderId;
}
