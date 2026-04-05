namespace TikTokOrderPrinter.Models;

public sealed class SellerCenterCaptureRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string BuyerNickname { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}
