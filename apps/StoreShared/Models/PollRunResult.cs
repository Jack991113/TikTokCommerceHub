namespace TikTokOrderPrinter.Models;

public sealed class PollRunResult
{
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int OrdersFound { get; set; }
    public int OrdersProcessed { get; set; }
    public int OrdersPrinted { get; set; }
    public int OrdersSkipped { get; set; }
    public int OrdersFailed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
}
