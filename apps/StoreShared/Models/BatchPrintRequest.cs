namespace TikTokOrderPrinter.Models;

public sealed class BatchPrintRequest
{
    public List<string> OrderIds { get; set; } = [];
}
