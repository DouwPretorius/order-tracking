namespace stock_api.Services;

public static class DuplicateSubmissionPolicy
{
    /// <summary>Determines whether a submission timestamp falls within the configured duplicate-blocking window.</summary>
    /// <param name="submittedAt">The timestamp of the earlier matching submission.</param>
    /// <param name="now">The current timestamp used as the comparison reference.</param>
    /// <param name="windowSeconds">The configured window in seconds; zero disables duplicate blocking.</param>
    /// <returns><see langword="true"/> when the earlier submission is inside or exactly at the window boundary.</returns>
    public static bool IsWithinWindow(DateTimeOffset submittedAt, DateTimeOffset now, int windowSeconds)
    {
        if (windowSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "The duplicate window cannot be negative.");
        }

        return windowSeconds > 0 && submittedAt >= now.AddSeconds(-windowSeconds);
    }
}
