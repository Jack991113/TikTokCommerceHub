namespace TikTokOrderPrinter.Models;

public sealed class OrderItemPrintModel
{
    public string Title { get; set; } = "Unknown item";
    public string Variant { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
}
