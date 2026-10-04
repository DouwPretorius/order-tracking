namespace stock_api.Services;

public sealed class OrderSubmissionOptions
{
    public const string SectionName = "OrderSubmission";

    public int DuplicateWindowSeconds { get; set; } = 10;
}
