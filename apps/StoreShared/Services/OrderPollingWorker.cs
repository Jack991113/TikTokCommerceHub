using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TikTokOrderPrinter.Models;
using TikTokOrderPrinter.Options;

namespace TikTokOrderPrinter.Services;

public sealed class OrderPollingWorker : BackgroundService
{
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private string _lastPollFailureMessage = string.Empty;
    private readonly RuntimeStateStore _stateStore;
    private readonly ServiceStatusStore _statusStore;
    private readonly TikTokShopClient _client;
    private readonly OrderPayloadMapper _mapper;
    private readonly OrderTicketRenderer _renderer;
    private readonly WindowsPrintService _printer;
    private readonly TikTokShopOptions _options;
    private readonly ILogger<OrderPollingWorker> _logger;

    public OrderPollingWorker(
        RuntimeStateStore stateStore,
        ServiceStatusStore statusStore,
        TikTokShopClient client,
        OrderPayloadMapper mapper,
        OrderTicketRenderer renderer,
        WindowsPrintService printer,
        IOptions<TikTokShopOptions> options,
        ILogger<OrderPollingWorker> logger)
    {
        _stateStore = stateStore;
        _statusStore = statusStore;
        _client = client;
        _mapper = mapper;
        _renderer = renderer;
        _printer = printer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PollRunResult> RunPollAsync(CancellationToken cancellationToken)
    {
        await _pollLock.WaitAsync(cancellationToken);
        var result = new PollRunResult
        {
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        _statusStore.MarkPollStarted(result.StartedAtUtc);

        try
        {
            await EnsureAccessTokenReadyAsync(cancellationToken);

            var state = await _stateStore.GetSnapshotAsync(cancellationToken);
            var fromUtc = state.LastPollCompletedAtUtc?.AddMinutes(-2)
                          ?? DateTimeOffset.UtcNow.AddMinutes(-_options.OrderLookbackMinutes);
            var toUtc = DateTimeOffset.UtcNow;

            var listResponse = await _client.SearchOrdersAsync(fromUtc, toUtc, cancellationToken);
            var orderIds = _mapper.ExtractOrderIds(listResponse)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.OrdersFound = orderIds.Count;

            foreach (var orderId in orderIds)
            {
                var existingRecord = await _stateStore.GetOrderAsync(orderId, cancellationToken);
                var alreadyPrintedEligibleOrder = existingRecord?.PrintedAtUtc is not null &&
                                                  OrderPrintEligibility.CanAutoPrint(existingRecord);
                if (alreadyPrintedEligibleOrder)
                {
                    result.OrdersSkipped++;
                    continue;
                }

                try
                {
                    var detailResponse = await _client.GetOrderDetailAsync(orderId, cancellationToken);
                    var order = _mapper.MapOrder(detailResponse);
                    var artifacts = await _renderer.CreateArtifactsAsync(order, detailResponse, cancellationToken);
                    var shouldAutoPrint = state.AutoPrintNewOrders && OrderPrintEligibility.CanAutoPrint(order);
                    var shouldWaitForBridgeCapture = shouldAutoPrint &&
                                                     state.AutoPrintAfterBridgeCapture &&
                                                     string.IsNullOrWhiteSpace(order.BuyerAccountName);
                    DateTimeOffset? printedAtUtc = null;
                    var printError = string.Empty;
                    var printCount = 0;

                    if (shouldAutoPrint && !shouldWaitForBridgeCapture)
                    {
                        try
                        {
                            printedAtUtc = await _printer.PrintAsync(artifacts, cancellationToken);
                            if (printedAtUtc is not null)
                            {
                                result.OrdersPrinted++;
                                printCount = (existingRecord?.PrintCount ?? 0) + 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            printError = ex.Message;
                            _logger.LogError(ex, "Auto printing failed for order {OrderId}", orderId);
                        }
                    }

                    await _stateStore.MarkProcessedAsync(
                        CreateRecord(
                            order,
                            artifacts,
                            existingRecord?.ProcessedAtUtc ?? DateTimeOffset.UtcNow,
                            printedAtUtc,
                            printError,
                            existingRecord,
                            printCount),
                        cancellationToken);

                    result.OrdersProcessed++;
                }
                catch (Exception ex)
                {
                    result.OrdersFailed++;
                    _logger.LogError(ex, "Failed to sync TikTok order {OrderId}", orderId);
                }
            }

            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _stateStore.UpdateLastPollCompletedAsync(result.CompletedAtUtc, cancellationToken);
            _statusStore.MarkPollCompleted(result);
            _lastPollFailureMessage = string.Empty;
            return result;
        }
        catch (Exception ex)
        {
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            result.ErrorMessage = ex.Message;
            _statusStore.MarkPollCompleted(result);
            LogPollFailure(ex);
            return result;
        }
        finally
        {
            _pollLock.Release();
        }
    }

    public async Task<bool> ReprintAsync(string orderId, CancellationToken cancellationToken)
    {
        var result = await PrintOrderAsync(orderId, forceFresh: false, allowReprint: true, cancellationToken);
        return result is not null;
    }

    public async Task<PrintedOrderRecord?> PrintOrderAsync(
        string orderId,
        bool forceFresh,
        bool allowReprint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return null;
        }

        var existing = await _stateStore.GetOrderAsync(orderId, cancellationToken);
        if (!allowReprint && existing?.PrintedAtUtc is not null)
        {
            throw new InvalidOperationException(OrderPrintEligibility.GetBlockingReason(existing.Status, existing.PaidAtUtc, alreadyPrinted: true));
        }

        JsonObject? rawPayload = null;
        OrderPrintModel? order = null;

        if (!forceFresh &&
            existing is not null &&
            !string.IsNullOrWhiteSpace(existing.PayloadFilePath) &&
            File.Exists(existing.PayloadFilePath))
        {
            var rawPayloadText = await File.ReadAllTextAsync(existing.PayloadFilePath, cancellationToken);
            rawPayload = JsonNode.Parse(rawPayloadText) as JsonObject;
            if (rawPayload is not null)
            {
                order = _mapper.MapOrder(rawPayload);
            }
        }

        if (rawPayload is null || order is null)
        {
            await EnsureAccessTokenReadyAsync(cancellationToken);
            rawPayload = await _client.GetOrderDetailAsync(orderId, cancellationToken);
            order = _mapper.MapOrder(rawPayload);
        }

        var apiBuyerAccountName = order.BuyerAccountName;
        _mapper.ApplyRecordOverlay(order, existing);
        if (!allowReprint && !OrderPrintEligibility.CanAutoPrint(order))
        {
            throw new InvalidOperationException(OrderPrintEligibility.GetBlockingReason(order.Status, order.PaidAtUtc, alreadyPrinted: false));
        }

        var artifacts = await _renderer.CreateArtifactsAsync(order, rawPayload, cancellationToken);
        var printedAtUtc = await _printer.PrintAsync(artifacts, cancellationToken);
        var nextRecord = new PrintedOrderRecord
        {
            OrderId = order.OrderId,
            DisplayName = order.DisplayName,
            BuyerAccountName = order.BuyerAccountName,
            BuyerAccountNameSource = ResolveBuyerAccountNameSource(existing, apiBuyerAccountName, order.BuyerAccountName),
            BuyerAccountNameSourceUrl = existing?.BuyerAccountNameSourceUrl ?? string.Empty,
            BuyerAccountNameCapturedAtUtc = existing?.BuyerAccountNameCapturedAtUtc,
            BuyerPlatformUserId = order.BuyerPlatformUserId,
            BuyerName = order.BuyerName,
            BuyerEmail = order.BuyerEmail,
            RecipientName = order.RecipientName,
            RecipientPhone = order.RecipientPhone,
            RecipientAddress = order.RecipientAddress,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            PaidAtUtc = order.PaidAtUtc,
            ProcessedAtUtc = existing?.ProcessedAtUtc ?? DateTimeOffset.UtcNow,
            PrintedAtUtc = printedAtUtc,
            PrintCount = (existing?.PrintCount ?? 0) + (printedAtUtc is null ? 0 : 1),
            TicketFilePath = artifacts.TicketFilePath,
            PayloadFilePath = artifacts.PayloadFilePath,
            TicketContent = artifacts.TicketContent,
            PrintError = string.Empty
        };

        await _stateStore.MarkProcessedAsync(nextRecord, cancellationToken);
        return nextRecord;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunPollAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, _options.PollIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunPollAsync(stoppingToken);
        }
    }

    private async Task EnsureAccessTokenReadyAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoRefreshAccessToken)
        {
            return;
        }

        var state = await _stateStore.GetSnapshotAsync(cancellationToken);
        var accessToken = string.IsNullOrWhiteSpace(state.AccessToken) ? _options.AccessToken : state.AccessToken;
        var expiresAtUtc = state.AccessTokenExpiresAtUtc;

        var shouldRefresh = string.IsNullOrWhiteSpace(accessToken)
                            || (expiresAtUtc is not null && expiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(_options.RefreshEarlyMinutes));

        if (!shouldRefresh)
        {
            return;
        }

        await _client.RefreshAccessTokenAsync(cancellationToken);
    }

    private void LogPollFailure(Exception ex)
    {
        var message = ex.Message;
        var isSetupIssue = ex is InvalidOperationException && IsExpectedSetupIssue(message);
        var isRepeated = string.Equals(_lastPollFailureMessage, message, StringComparison.Ordinal);

        if (isSetupIssue)
        {
            if (isRepeated)
            {
                _logger.LogDebug("TikTok order polling is still waiting for setup: {Message}", message);
            }
            else
            {
                _logger.LogInformation("TikTok order polling is waiting for setup: {Message}", message);
            }

            _lastPollFailureMessage = message;
            return;
        }

        if (isRepeated)
        {
            _logger.LogWarning("TikTok order polling is still failing: {Message}", message);
            return;
        }

        _lastPollFailureMessage = message;
        _logger.LogError(ex, "TikTok order polling failed.");
    }

    private static PrintedOrderRecord CreateRecord(
        OrderPrintModel order,
        OrderPrintArtifacts artifacts,
        DateTimeOffset processedAtUtc,
        DateTimeOffset? printedAtUtc,
        string printError,
        PrintedOrderRecord? existingRecord,
        int printCount) =>
        new()
        {
            OrderId = order.OrderId,
            DisplayName = order.DisplayName,
            BuyerAccountName = order.BuyerAccountName,
            BuyerAccountNameSource = string.IsNullOrWhiteSpace(order.BuyerAccountName)
                ? existingRecord?.BuyerAccountNameSource ?? string.Empty
                : "api_order_detail",
            BuyerAccountNameSourceUrl = existingRecord?.BuyerAccountNameSourceUrl ?? string.Empty,
            BuyerAccountNameCapturedAtUtc = existingRecord?.BuyerAccountNameCapturedAtUtc,
            BuyerPlatformUserId = order.BuyerPlatformUserId,
            BuyerName = order.BuyerName,
            BuyerEmail = order.BuyerEmail,
            RecipientName = order.RecipientName,
            RecipientPhone = order.RecipientPhone,
            RecipientAddress = order.RecipientAddress,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            PaidAtUtc = order.PaidAtUtc,
            ProcessedAtUtc = processedAtUtc,
            PrintedAtUtc = printedAtUtc,
            PrintCount = printCount,
            TicketFilePath = artifacts.TicketFilePath,
            PayloadFilePath = artifacts.PayloadFilePath,
            TicketContent = artifacts.TicketContent,
            PrintError = printError
        };

    private static string ResolveBuyerAccountNameSource(
        PrintedOrderRecord? existing,
        string apiBuyerAccountName,
        string finalBuyerAccountName)
    {
        if (existing is not null &&
            string.Equals(existing.BuyerAccountNameSource, "seller_center_bridge", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(existing.BuyerAccountName) &&
            string.IsNullOrWhiteSpace(apiBuyerAccountName) &&
            string.Equals(existing.BuyerAccountName, finalBuyerAccountName, StringComparison.OrdinalIgnoreCase))
        {
            return existing.BuyerAccountNameSource;
        }

        if (!string.IsNullOrWhiteSpace(apiBuyerAccountName))
        {
            return "api_order_detail";
        }

        return existing?.BuyerAccountNameSource ?? string.Empty;
    }

    private static bool IsExpectedSetupIssue(string message) =>
        message.Contains("\u7F3A\u5C11 Access Token", StringComparison.Ordinal) ||
        message.Contains("\u7F3A\u5C11 Refresh Token", StringComparison.Ordinal) ||
        message.Contains("\u7F3A\u5C11 App Key", StringComparison.Ordinal) ||
        message.Contains("\u7F3A\u5C11 App Secret", StringComparison.Ordinal) ||
        message.Contains("\u7F3A\u5C11 Shop ID", StringComparison.Ordinal) ||
        message.Contains("\u6388\u6743\u7801\u4E3A\u7A7A", StringComparison.Ordinal) ||
        message.Contains("Fill Shop ID / Shop Cipher", StringComparison.Ordinal) ||
        message.Contains("Fill Shop ID or Shop Cipher", StringComparison.Ordinal) ||
        message.Contains("No authorized TikTok shops", StringComparison.Ordinal) ||
        message.Contains("Unable to resolve shop_cipher", StringComparison.Ordinal) ||
        message.Contains("Unable to resolve TikTok shop cipher", StringComparison.Ordinal) ||
        message.Contains("cannot call Get Authorized Shops", StringComparison.Ordinal) ||
        message.Contains("cannot convert a numeric Shop ID into shop_cipher", StringComparison.Ordinal) ||
        message.Contains("Paste the actual shop_cipher", StringComparison.Ordinal);
}
