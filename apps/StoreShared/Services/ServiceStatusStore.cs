using TikTokOrderPrinter.Models;

namespace TikTokOrderPrinter.Services;

public sealed class ServiceStatusStore
{
    private readonly object _sync = new();
    private readonly DateTimeOffset _serviceStartedAtUtc = DateTimeOffset.UtcNow;

    private bool _isPolling;
    private DateTimeOffset? _lastPollStartedAtUtc;
    private DateTimeOffset? _lastPollCompletedAtUtc;
    private string _lastError = string.Empty;
    private int _lastOrdersFound;
    private int _lastOrdersProcessed;
    private int _lastOrdersPrinted;
    private int _lastOrdersSkipped;
    private int _lastOrdersFailed;
    private DateTimeOffset? _lastBridgeHeartbeatAtUtc;
    private string _lastBridgeSourceUrl = string.Empty;
    private DateTimeOffset? _lastBridgeCaptureAtUtc;
    private string _lastBridgeOrderId = string.Empty;
    private string _lastBridgeBuyerNickname = string.Empty;

    public void MarkPollStarted(DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _isPolling = true;
            _lastPollStartedAtUtc = startedAtUtc;
            _lastError = string.Empty;
        }
    }

    public void MarkPollCompleted(PollRunResult result)
    {
        lock (_sync)
        {
            _isPolling = false;
            _lastPollCompletedAtUtc = result.CompletedAtUtc;
            _lastOrdersFound = result.OrdersFound;
            _lastOrdersProcessed = result.OrdersProcessed;
            _lastOrdersPrinted = result.OrdersPrinted;
            _lastOrdersSkipped = result.OrdersSkipped;
            _lastOrdersFailed = result.OrdersFailed;
            _lastError = result.ErrorMessage;
        }
    }

    public ServiceStatusSnapshot GetSnapshot(int totalProcessedOrders, DateTimeOffset? lastProcessedOrderAtUtc)
    {
        lock (_sync)
        {
            return new ServiceStatusSnapshot
            {
                ServiceStartedAtUtc = _serviceStartedAtUtc,
                IsPolling = _isPolling,
                LastPollStartedAtUtc = _lastPollStartedAtUtc,
                LastPollCompletedAtUtc = _lastPollCompletedAtUtc,
                LastError = _lastError,
                LastOrdersFound = _lastOrdersFound,
                LastOrdersProcessed = _lastOrdersProcessed,
                LastOrdersPrinted = _lastOrdersPrinted,
                LastOrdersSkipped = _lastOrdersSkipped,
                LastOrdersFailed = _lastOrdersFailed,
                TotalProcessedOrders = totalProcessedOrders,
                LastProcessedOrderAtUtc = lastProcessedOrderAtUtc,
                LastBridgeHeartbeatAtUtc = _lastBridgeHeartbeatAtUtc,
                LastBridgeSourceUrl = _lastBridgeSourceUrl,
                LastBridgeCaptureAtUtc = _lastBridgeCaptureAtUtc,
                LastBridgeOrderId = _lastBridgeOrderId,
                LastBridgeBuyerNickname = _lastBridgeBuyerNickname
            };
        }
    }

    public void MarkBridgeHeartbeat(DateTimeOffset heartbeatAtUtc, string sourceUrl)
    {
        lock (_sync)
        {
            _lastBridgeHeartbeatAtUtc = heartbeatAtUtc;
            _lastBridgeSourceUrl = sourceUrl;
        }
    }

    public void MarkBridgeCapture(DateTimeOffset capturedAtUtc, string orderId, string buyerNickname, string sourceUrl)
    {
        lock (_sync)
        {
            _lastBridgeHeartbeatAtUtc = capturedAtUtc;
            _lastBridgeSourceUrl = sourceUrl;
            _lastBridgeCaptureAtUtc = capturedAtUtc;
            _lastBridgeOrderId = orderId;
            _lastBridgeBuyerNickname = buyerNickname;
        }
    }
}
