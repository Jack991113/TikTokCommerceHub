namespace TikTokOrderPrinter.Options;

public sealed class PrintingOptions
{
    public const string SectionName = "Printing";

    public bool Enabled { get; set; } = true;
    public string PrinterName { get; set; } = string.Empty;
    public bool SaveArtifacts { get; set; } = true;
    public int PaperWidthCharacters { get; set; } = 42;
    public string PaperSize { get; set; } = "100x150";
    public double CustomPaperWidthMm { get; set; } = 100;
    public double CustomPaperHeightMm { get; set; } = 150;
    public double MarginMm { get; set; } = 2.5d;
    public float BaseFontSize { get; set; } = 8.6f;
    public float MinFontSize { get; set; } = 6.3f;
    public bool AutoPrintNewOrders { get; set; }
    public bool AutoPrintAfterBridgeCapture { get; set; }
    public bool ShowBuyerAccountName { get; set; } = true;
    public bool ShowBuyerPlatformUserId { get; set; } = true;
    public bool ShowBuyerName { get; set; } = true;
    public bool ShowBuyerEmail { get; set; }
    public bool ShowRecipientPhone { get; set; } = true;
    public bool ShowBuyerMessage { get; set; } = true;
    public bool ShowOrderAmounts { get; set; } = true;
    public bool ShowItemDetails { get; set; } = true;
    public bool ShowSku { get; set; }
    public bool ShowPaidTime { get; set; } = true;
    public bool ShowCreatedTime { get; set; } = true;
    public List<string> SelectedRawFieldPaths { get; set; } = [];
}

