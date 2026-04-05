namespace TikTokOrderPrinter.Models;

public sealed class OrderQueryRequest
{
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public int PageSize { get; set; } = 30;
    public string PageToken { get; set; } = string.Empty;
    public string SortField { get; set; } = "create_time";
    public string SortOrder { get; set; } = "DESC";
    public string Keyword { get; set; } = string.Empty;
    public List<string> Statuses { get; set; } = [];
}
