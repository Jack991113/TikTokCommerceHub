namespace TikTokOrderPrinter.Models;

public sealed class OrderListItem
{
    public string OrderId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsCached { get; set; }
    public bool HasLocalPayload { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BuyerAccountName { get; set; } = string.Empty;
    public string BuyerAccountNameSource { get; set; } = string.Empty;
    public DateTimeOffset? BuyerAccountNameCapturedAtUtc { get; set; }
    public string BuyerPlatformUserId { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public DateTimeOffset? PrintedAtUtc { get; set; }
    public int PrintCount { get; set; }
    public string PrintError { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public string PrimaryItemSummary { get; set; } = string.Empty;
}
