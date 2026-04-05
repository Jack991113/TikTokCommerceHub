namespace TikTokOrderPrinter.Models;

public sealed class OrderPrintModel
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public string BuyerAccountName { get; set; } = string.Empty;
    public string BuyerPlatformUserId { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string BuyerMessage { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<OrderItemPrintModel> Items { get; set; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(RecipientName) ? BuyerName : RecipientName;
}
