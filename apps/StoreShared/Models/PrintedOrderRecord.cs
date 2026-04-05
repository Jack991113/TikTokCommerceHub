namespace TikTokOrderPrinter.Models;

public sealed class PrintedOrderRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BuyerAccountName { get; set; } = string.Empty;
    public string BuyerAccountNameSource { get; set; } = string.Empty;
    public string BuyerAccountNameSourceUrl { get; set; } = string.Empty;
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
    public DateTimeOffset ProcessedAtUtc { get; set; }
    public DateTimeOffset? PrintedAtUtc { get; set; }
    public int PrintCount { get; set; }
    public string TicketFilePath { get; set; } = string.Empty;
    public string PayloadFilePath { get; set; } = string.Empty;
    public string TicketContent { get; set; } = string.Empty;
    public string PrintError { get; set; } = string.Empty;
}
