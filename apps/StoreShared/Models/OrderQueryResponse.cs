namespace TikTokOrderPrinter.Models;

public sealed class OrderQueryResponse
{
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public int PageSize { get; set; }
    public string PageToken { get; set; } = string.Empty;
    public string NextPageToken { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int ReturnedCount { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public List<string> Statuses { get; set; } = [];
    public List<OrderListItem> Orders { get; set; } = [];
}
