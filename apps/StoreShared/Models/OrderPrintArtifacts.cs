namespace TikTokOrderPrinter.Models;

public sealed class OrderPrintArtifacts
{
    public required string OrderId { get; init; }
    public required string TicketFilePath { get; init; }
    public required string PayloadFilePath { get; init; }
    public required string TicketContent { get; init; }
}
