namespace TikTokOrderPrinter.Models;

public sealed class LocalAppConfiguration
{
    public string StoreName { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public string PaperSize { get; set; } = string.Empty;
    public double? CustomPaperWidthMm { get; set; }
    public double? CustomPaperHeightMm { get; set; }
    public double? MarginMm { get; set; }
    public int? PaperWidthCharacters { get; set; }
    public float? BaseFontSize { get; set; }
    public float? MinFontSize { get; set; }
    public bool AutoPrintNewOrders { get; set; }
    public bool AutoPrintAfterBridgeCapture { get; set; }
    public bool? ShowBuyerAccountName { get; set; }
    public bool? ShowBuyerPlatformUserId { get; set; }
    public bool? ShowBuyerName { get; set; }
    public bool? ShowBuyerEmail { get; set; }
    public bool? ShowRecipientPhone { get; set; }
    public bool? ShowBuyerMessage { get; set; }
    public bool? ShowOrderAmounts { get; set; }
    public bool? ShowItemDetails { get; set; }
    public bool? ShowSku { get; set; }
    public bool? ShowPaidTime { get; set; }
    public bool? ShowCreatedTime { get; set; }
    public List<string> SelectedRawFieldPaths { get; set; } = [];
}
