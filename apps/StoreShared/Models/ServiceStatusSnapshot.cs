namespace TikTokOrderPrinter.Models;

public sealed class ServiceStatusSnapshot
{
    public DateTimeOffset ServiceStartedAtUtc { get; set; }
    public bool IsPolling { get; set; }
    public DateTimeOffset? LastPollStartedAtUtc { get; set; }
    public DateTimeOffset? LastPollCompletedAtUtc { get; set; }
    public string LastError { get; set; } = string.Empty;
    public int LastOrdersFound { get; set; }
    public int LastOrdersProcessed { get; set; }
    public int LastOrdersPrinted { get; set; }
    public int LastOrdersSkipped { get; set; }
    public int LastOrdersFailed { get; set; }
    public int TotalProcessedOrders { get; set; }
    public DateTimeOffset? LastProcessedOrderAtUtc { get; set; }
    public DateTimeOffset? LastBridgeHeartbeatAtUtc { get; set; }
    public string LastBridgeSourceUrl { get; set; } = string.Empty;
    public DateTimeOffset? LastBridgeCaptureAtUtc { get; set; }
    public string LastBridgeOrderId { get; set; } = string.Empty;
    public string LastBridgeBuyerNickname { get; set; } = string.Empty;
    public int BridgePendingCount { get; set; }
    public int BridgeMatchedCount { get; set; }
    public DateTimeOffset? LatestPendingBridgeOrderAtUtc { get; set; }
    public string LatestPendingBridgeOrderId { get; set; } = string.Empty;
}
