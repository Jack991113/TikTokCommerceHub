using System.Drawing.Printing;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TikTokOrderPrinter.Models;
using TikTokOrderPrinter.Options;
using TikTokOrderPrinter.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<TikTokShopOptions>()
    .Bind(builder.Configuration.GetSection(TikTokShopOptions.SectionName));
builder.Services.AddOptions<PrintingOptions>()
    .Bind(builder.Configuration.GetSection(PrintingOptions.SectionName));
builder.Services.AddOptions<AppDataOptions>()
    .Bind(builder.Configuration.GetSection(AppDataOptions.SectionName));

builder.Services.AddHttpClient("TikTokApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestVersion = HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
});
builder.Services.AddSingleton<RuntimeStateStore>();
builder.Services.AddSingleton<ServiceStatusStore>();
builder.Services.AddSingleton<TikTokRequestSigner>();
builder.Services.AddSingleton<TikTokShopClient>();
builder.Services.AddSingleton<OrderPayloadMapper>();
builder.Services.AddSingleton<OrderTicketRenderer>();
builder.Services.AddSingleton<WindowsPrintService>();
builder.Services.AddSingleton<OrderPollingWorker>();
builder.Services.AddHostedService(static sp => sp.GetRequiredService<OrderPollingWorker>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", async (
    RuntimeStateStore stateStore,
    IOptions<TikTokShopOptions> tikTokOptions,
    IOptions<PrintingOptions> printingOptions,
    CancellationToken cancellationToken) =>
{
    var saved = await stateStore.GetConfigurationAsync(cancellationToken);
    var defaults = printingOptions.Value;

    var response = new LocalAppConfiguration
    {
        StoreName = saved.StoreName,
        AppKey = FirstNonEmpty(saved.AppKey, tikTokOptions.Value.AppKey),
        AppSecret = FirstNonEmpty(saved.AppSecret, tikTokOptions.Value.AppSecret),
        AccessToken = FirstNonEmpty(saved.AccessToken, tikTokOptions.Value.AccessToken),
        RefreshToken = FirstNonEmpty(saved.RefreshToken, tikTokOptions.Value.RefreshToken),
        ShopId = FirstNonEmpty(saved.ShopId, tikTokOptions.Value.ShopId),
        PrinterName = FirstNonEmpty(saved.PrinterName, defaults.PrinterName),
        PaperSize = FirstNonEmpty(saved.PaperSize, defaults.PaperSize),
        CustomPaperWidthMm = FirstPositiveDouble(saved.CustomPaperWidthMm, defaults.CustomPaperWidthMm),
        CustomPaperHeightMm = FirstPositiveDouble(saved.CustomPaperHeightMm, defaults.CustomPaperHeightMm),
        MarginMm = FirstPositiveDouble(saved.MarginMm, defaults.MarginMm),
        PaperWidthCharacters = FirstPositiveInt(saved.PaperWidthCharacters, defaults.PaperWidthCharacters),
        BaseFontSize = FirstPositiveFloat(saved.BaseFontSize, defaults.BaseFontSize),
        MinFontSize = FirstPositiveFloat(saved.MinFontSize, defaults.MinFontSize),
        AutoPrintNewOrders = saved.AutoPrintNewOrders || defaults.AutoPrintNewOrders,
        AutoPrintAfterBridgeCapture = saved.AutoPrintAfterBridgeCapture || defaults.AutoPrintAfterBridgeCapture,
        ShowBuyerAccountName = Resolve(saved.ShowBuyerAccountName, defaults.ShowBuyerAccountName),
        ShowBuyerPlatformUserId = Resolve(saved.ShowBuyerPlatformUserId, defaults.ShowBuyerPlatformUserId),
        ShowBuyerName = Resolve(saved.ShowBuyerName, defaults.ShowBuyerName),
        ShowBuyerEmail = Resolve(saved.ShowBuyerEmail, defaults.ShowBuyerEmail),
        ShowRecipientPhone = Resolve(saved.ShowRecipientPhone, defaults.ShowRecipientPhone),
        ShowBuyerMessage = Resolve(saved.ShowBuyerMessage, defaults.ShowBuyerMessage),
        ShowOrderAmounts = Resolve(saved.ShowOrderAmounts, defaults.ShowOrderAmounts),
        ShowItemDetails = Resolve(saved.ShowItemDetails, defaults.ShowItemDetails),
        ShowSku = Resolve(saved.ShowSku, defaults.ShowSku),
        ShowPaidTime = Resolve(saved.ShowPaidTime, defaults.ShowPaidTime),
        ShowCreatedTime = Resolve(saved.ShowCreatedTime, defaults.ShowCreatedTime),
        SelectedRawFieldPaths = saved.SelectedRawFieldPaths.Count > 0
            ? saved.SelectedRawFieldPaths
            : defaults.SelectedRawFieldPaths
    };

    return Results.Json(response);
});

app.MapPost("/api/config", async (LocalAppConfiguration configuration, RuntimeStateStore stateStore, CancellationToken cancellationToken) =>
{
    await stateStore.SaveConfigurationAsync(configuration, cancellationToken);
    return Results.Ok(new { success = true, message = "配置已保存。" });
});

app.MapPost("/api/token/exchange", async (AuthCodeExchangeRequest request, TikTokShopClient client, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.AuthCodeOrUrl))
    {
        return Results.BadRequest(new { success = false, message = "请先粘贴授权码，或粘贴完整回调地址。" });
    }

    try
    {
        var tokenResult = await client.ExchangeAuthorizationCodeAsync(request.AuthCodeOrUrl, cancellationToken);
        return Results.Ok(tokenResult);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/printers", () =>
{
    if (!OperatingSystem.IsWindows())
    {
        return Results.Json(Array.Empty<string>());
    }

    var printers = PrinterSettings.InstalledPrinters
        .Cast<string>()
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return Results.Json(printers);
});

app.MapGet("/api/status", async (RuntimeStateStore stateStore, ServiceStatusStore statusStore, CancellationToken cancellationToken) =>
{
    var runtimeState = await stateStore.GetSnapshotAsync(cancellationToken);
    var status = BuildStatusSnapshot(runtimeState, statusStore);

    return Results.Json(status);
});

app.MapGet("/api/orders/recent", async (RuntimeStateStore stateStore, CancellationToken cancellationToken) =>
{
    var recentOrders = await stateStore.GetRecentOrdersAsync(50, cancellationToken);
    return Results.Json(recentOrders.Select(ToListItem));
});

app.MapPost("/api/orders/history", async (
    OrderQueryRequest request,
    TikTokShopClient client,
    OrderPayloadMapper mapper,
    RuntimeStateStore stateStore,
    CancellationToken cancellationToken) =>
{
    var fromUtc = request.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30);
    var toUtc = request.ToUtc ?? DateTimeOffset.UtcNow;
    if (fromUtc >= toUtc)
    {
        return Results.BadRequest(new { success = false, message = "历史订单查询的开始时间必须早于结束时间。" });
    }

    try
    {
        var normalizedRequest = new OrderQueryRequest
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            PageSize = Math.Clamp(request.PageSize, 10, 100),
            PageToken = request.PageToken?.Trim() ?? string.Empty,
            SortField = string.IsNullOrWhiteSpace(request.SortField) ? "create_time" : request.SortField.Trim(),
            SortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? "DESC" : request.SortOrder.Trim().ToUpperInvariant(),
            Keyword = request.Keyword?.Trim() ?? string.Empty,
            Statuses = request.Statuses
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status => status.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var searchResponse = await client.SearchOrdersPageAsync(normalizedRequest, cancellationToken);
        var data = searchResponse["data"] as JsonObject;
        var orders = data?["orders"] as JsonArray ?? new JsonArray();
        var snapshot = await stateStore.GetSnapshotAsync(cancellationToken);
        var cachedLookup = snapshot.ProcessedOrders
            .GroupBy(record => record.OrderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ProcessedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var mapped = orders
            .OfType<JsonObject>()
            .Select(orderNode =>
            {
                var orderId = ExtractJsonString(orderNode, "order_id") ?? ExtractJsonString(orderNode, "id") ?? string.Empty;
                cachedLookup.TryGetValue(orderId, out var cachedRecord);
        return mapper.MapListItem(orderNode, "history", cachedRecord);
            })
            .Where(item => MatchesStatus(item, normalizedRequest.Statuses))
            .Where(item => MatchesKeyword(item, normalizedRequest.Keyword))
            .OrderByDescending(item => item.PaidAtUtc ?? item.CreatedAtUtc ?? item.UpdatedAtUtc)
            .ThenByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .ToList();

        var response = new OrderQueryResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            PageSize = normalizedRequest.PageSize,
            PageToken = normalizedRequest.PageToken,
            NextPageToken = ExtractJsonString(data, "next_page_token") ?? string.Empty,
            TotalCount = data?["total_count"]?.GetValue<int?>() ?? mapped.Count,
            ReturnedCount = mapped.Count,
            Keyword = normalizedRequest.Keyword,
            Statuses = normalizedRequest.Statuses,
            Orders = mapped
        };

        return Results.Json(response);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/orders/{orderId}/preview", async (
    string orderId,
    RuntimeStateStore stateStore,
    OrderPayloadMapper mapper,
    OrderTicketRenderer renderer,
    TikTokShopClient client,
    CancellationToken cancellationToken) =>
{
    var record = await stateStore.GetOrderAsync(orderId, cancellationToken);
    JsonObject? payload = null;
    if (record is not null &&
        !string.IsNullOrWhiteSpace(record.PayloadFilePath) &&
        File.Exists(record.PayloadFilePath))
    {
        var payloadText = await File.ReadAllTextAsync(record.PayloadFilePath, cancellationToken);
        payload = JsonNode.Parse(payloadText) as JsonObject;
    }

    if (payload is null)
    {
        try
        {
            payload = await client.GetOrderDetailAsync(orderId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { success = false, message = $"没有找到这笔订单的预览数据，且实时拉取详情失败：{ex.Message}" });
        }
    }

    if (payload is null)
    {
        return Results.NotFound(new { success = false, message = "订单原始数据已损坏，无法生成预览。" });
    }

    var order = mapper.MapOrder(payload);
    mapper.ApplyRecordOverlay(order, record);
    var ticketContent = await renderer.BuildTicketContentAsync(order, payload, cancellationToken);
    var groupedItems = OrderItemGrouping.MergeLikeItems(order.Items);
    var rawOrder = GetPrimaryOrderNode(payload) ?? payload;
    var rawFields = TikTokApiFieldCatalog.Flatten(rawOrder);
    var buyerFields = rawFields
        .Where(field =>
            field.Path.Contains("buyer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Path, "user_id", StringComparison.OrdinalIgnoreCase) ||
            field.Path.EndsWith(".user_id", StringComparison.OrdinalIgnoreCase))
        .ToList();

    return Results.Json(new
    {
        order.OrderId,
        order.Status,
        order.BuyerAccountName,
        buyerAccountNameSource = record?.BuyerAccountNameSource ?? string.Empty,
        buyerAccountNameCapturedAtUtc = record?.BuyerAccountNameCapturedAtUtc,
        order.BuyerPlatformUserId,
        order.BuyerName,
        order.BuyerEmail,
        buyerEmailAlias = ToEmailAlias(order.BuyerEmail),
        order.RecipientName,
        order.RecipientPhone,
        order.RecipientAddress,
        order.BuyerMessage,
        order.TotalAmount,
        order.Currency,
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.PaidAtUtc,
        order.Items,
        groupedItems,
        groupedItemSummary = OrderItemGrouping.BuildCompactSummary(order.Items),
        groupedItemCount = groupedItems.Count,
        totalQuantity = OrderItemGrouping.GetTotalQuantity(order.Items),
        ticketContent,
        rawFieldCount = rawFields.Count,
        rawFields,
        buyerFieldCount = buyerFields.Count,
        buyerFields,
        rawOrderJson = ToPrettyJson(rawOrder),
        rawResponseJson = ToPrettyJson(payload),
        printedAtUtc = record?.PrintedAtUtc,
        printCount = record?.PrintCount ?? 0,
        processedAtUtc = record?.ProcessedAtUtc
    });
});

app.MapGet("/api/bridge/seller-center/capture", async (
    HttpRequest request,
    RuntimeStateStore stateStore,
    ServiceStatusStore statusStore,
    OrderPollingWorker worker,
    CancellationToken cancellationToken) =>
{
    var capture = new SellerCenterCaptureRequest
    {
        OrderId = request.Query["orderId"].ToString(),
        BuyerNickname = request.Query["buyerNickname"].ToString(),
        BuyerName = request.Query["buyerName"].ToString(),
        SourceUrl = request.Query["sourceUrl"].ToString()
    };

    if (string.IsNullOrWhiteSpace(capture.OrderId) || string.IsNullOrWhiteSpace(capture.BuyerNickname))
    {
        return Results.File(TrackingPixelGif(), "image/gif");
    }

    statusStore.MarkBridgeCapture(
        DateTimeOffset.UtcNow,
        capture.OrderId,
        capture.BuyerNickname,
        capture.SourceUrl);

    var mergedRecord = await stateStore.MergeSellerCenterCaptureAsync(
        capture.OrderId,
        capture.BuyerNickname,
        capture.BuyerName,
        capture.SourceUrl,
        cancellationToken);

    var configuration = await stateStore.GetConfigurationAsync(cancellationToken);
    if (configuration.AutoPrintNewOrders &&
        configuration.AutoPrintAfterBridgeCapture &&
        mergedRecord.PrintedAtUtc is null &&
        OrderPrintEligibility.CanAutoPrint(mergedRecord))
    {
        try
        {
            await worker.PrintOrderAsync(capture.OrderId, forceFresh: false, allowReprint: false, cancellationToken);
        }
        catch (Exception ex)
        {
            await stateStore.UpdatePrintResultAsync(capture.OrderId, null, ex.Message, cancellationToken);
        }
    }

    return Results.File(TrackingPixelGif(), "image/gif");
});

app.MapGet("/api/bridge/seller-center/ping", (HttpRequest request, ServiceStatusStore statusStore) =>
{
    var sourceUrl = request.Query["sourceUrl"].ToString();
    statusStore.MarkBridgeHeartbeat(DateTimeOffset.UtcNow, sourceUrl);
    return Results.File(TrackingPixelGif(), "image/gif");
});

app.MapGet("/api/bridge/seller-center/signal", async (
    RuntimeStateStore stateStore,
    ServiceStatusStore statusStore,
    CancellationToken cancellationToken) =>
{
    var runtimeState = await stateStore.GetSnapshotAsync(cancellationToken);
    var snapshot = BuildStatusSnapshot(runtimeState, statusStore);
    return Results.Json(BuildBridgeSignalPayload(snapshot));
});

app.MapGet("/api/bridge/seller-center/signal.js", async (
    HttpContext context,
    RuntimeStateStore stateStore,
    ServiceStatusStore statusStore,
    CancellationToken cancellationToken) =>
{
    var runtimeState = await stateStore.GetSnapshotAsync(cancellationToken);
    var snapshot = BuildStatusSnapshot(runtimeState, statusStore);
    var payload = BuildBridgeSignalPayload(snapshot);

    var json = JsonSerializer.Serialize(payload);
    context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    context.Response.Headers.Pragma = "no-cache";
    return Results.Text($"window.__printStudioBridgeSignal = {json};", "application/javascript; charset=utf-8");
});

app.MapGet("/seller-center-bridge.user.js", (HttpContext context) =>
{
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
    var script = SellerCenterBridgeScriptBuilder.Build(baseUrl);
    return Results.Text(script, "application/javascript; charset=utf-8");
});

app.MapPost("/api/poll", async (OrderPollingWorker worker, CancellationToken cancellationToken) =>
{
    var result = await worker.RunPollAsync(cancellationToken);
    return Results.Json(result);
});

app.MapPost("/api/orders/{orderId}/reprint", async (string orderId, OrderPollingWorker worker, CancellationToken cancellationToken) =>
{
    try
    {
        var success = await worker.ReprintAsync(orderId, cancellationToken);
        return success
            ? Results.Ok(new { orderId, success = true })
            : Results.NotFound(new { orderId, success = false, message = "没有找到这笔订单的打印数据。" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { orderId, success = false, message = ex.Message });
    }
});

app.MapPost("/api/orders/{orderId}/print", async (
    string orderId,
    bool? fresh,
    OrderPollingWorker worker,
    CancellationToken cancellationToken) =>
{
    try
    {
        var record = await worker.PrintOrderAsync(orderId, fresh is true, allowReprint: false, cancellationToken);
        return record is null
            ? Results.NotFound(new { orderId, success = false, message = "没有找到这笔订单的打印数据。" })
            : Results.Ok(new
            {
                orderId,
                success = true,
                printedAtUtc = record.PrintedAtUtc,
                printCount = record.PrintCount
            });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { orderId, success = false, message = ex.Message });
    }
});

app.MapPost("/api/orders/print-selected", async (BatchPrintRequest request, OrderPollingWorker worker, CancellationToken cancellationToken) =>
{
    var orderIds = request.OrderIds
        .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (orderIds.Count == 0)
    {
        return Results.BadRequest(new { success = false, message = "请先选择要打印的订单。" });
    }

    var successIds = new List<string>();
    var failed = new List<object>();

    foreach (var orderId in orderIds)
    {
        try
        {
            var printed = await worker.PrintOrderAsync(orderId, forceFresh: false, allowReprint: false, cancellationToken);
            if (printed is not null)
            {
                successIds.Add(orderId);
            }
            else
            {
                failed.Add(new { orderId, message = "没有找到这笔订单的打印数据。" });
            }
        }
        catch (Exception ex)
        {
            failed.Add(new { orderId, message = ex.Message });
        }
    }

    return Results.Json(new
    {
        success = failed.Count == 0,
        requested = orderIds.Count,
        printed = successIds.Count,
        successIds,
        failed
    });
});

app.MapPost("/api/token/refresh", async (TikTokShopClient client, CancellationToken cancellationToken) =>
{
    try
    {
        var tokenResult = await client.RefreshAccessTokenAsync(cancellationToken);
        return tokenResult is null
            ? Results.BadRequest(new { success = false, message = "缺少 Refresh Token，无法刷新。" })
            : Results.Ok(tokenResult);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

app.Run();

static string FirstNonEmpty(string? first, string second) =>
    string.IsNullOrWhiteSpace(first) ? second : first;

static double FirstPositiveDouble(double? first, double second) =>
    first is > 0d ? first.Value : second;

static int FirstPositiveInt(int? first, int second) =>
    first is > 0 ? first.Value : second;

static float FirstPositiveFloat(float? first, float second) =>
    first is > 0f ? first.Value : second;

static bool Resolve(bool? value, bool fallback) =>
    value ?? fallback;

static string ToEmailAlias(string email)
{
    if (string.IsNullOrWhiteSpace(email))
    {
        return string.Empty;
    }

    var atIndex = email.IndexOf('@');
    return atIndex > 0 ? email[..atIndex] : email;
}

static OrderListItem ToListItem(PrintedOrderRecord record) =>
    new()
    {
        OrderId = record.OrderId,
        Source = "cache",
        IsCached = true,
        HasLocalPayload = !string.IsNullOrWhiteSpace(record.PayloadFilePath) && File.Exists(record.PayloadFilePath),
        DisplayName = record.DisplayName,
        BuyerAccountName = record.BuyerAccountName,
        BuyerAccountNameSource = record.BuyerAccountNameSource,
        BuyerAccountNameCapturedAtUtc = record.BuyerAccountNameCapturedAtUtc,
        BuyerPlatformUserId = record.BuyerPlatformUserId,
        BuyerName = record.BuyerName,
        BuyerEmail = record.BuyerEmail,
        RecipientName = record.RecipientName,
        RecipientPhone = record.RecipientPhone,
        RecipientAddress = record.RecipientAddress,
        Status = record.Status,
        TotalAmount = record.TotalAmount,
        Currency = record.Currency,
        CreatedAtUtc = record.CreatedAtUtc,
        UpdatedAtUtc = record.UpdatedAtUtc,
        PaidAtUtc = record.PaidAtUtc,
        ProcessedAtUtc = record.ProcessedAtUtc,
        PrintedAtUtc = record.PrintedAtUtc,
        PrintCount = record.PrintCount,
        PrintError = record.PrintError,
        PrimaryItemSummary = string.Empty
    };

static bool MatchesStatus(OrderListItem item, IReadOnlyCollection<string> statuses) =>
    statuses.Count == 0 ||
    statuses.Contains(item.Status, StringComparer.OrdinalIgnoreCase);

static bool MatchesKeyword(OrderListItem item, string keyword)
{
    if (string.IsNullOrWhiteSpace(keyword))
    {
        return true;
    }

    var text = keyword.Trim();
    var values = new[]
    {
        item.OrderId,
        item.BuyerAccountName,
        item.BuyerPlatformUserId,
        item.BuyerName,
        item.BuyerEmail,
        item.RecipientName,
        item.RecipientPhone,
        item.RecipientAddress,
        item.PrimaryItemSummary,
        item.Status
    };

    return values.Any(value => !string.IsNullOrWhiteSpace(value) && value.Contains(text, StringComparison.OrdinalIgnoreCase));
}

static string? ExtractJsonString(JsonObject? node, string propertyName) =>
    node?[propertyName]?.GetValue<string?>() ?? node?[propertyName]?.ToString();

static byte[] TrackingPixelGif() =>
    Convert.FromBase64String("R0lGODlhAQABAPAAAP///wAAACH5BAAAAAAALAAAAAABAAEAAAICRAEAOw==");

static JsonNode? GetPrimaryOrderNode(JsonObject payload)
{
    if (payload["data"] is JsonObject data &&
        data["orders"] is JsonArray orders &&
        orders.Count > 0)
    {
        return orders[0];
    }

    return payload;
}

static string ToPrettyJson(JsonNode node) =>
    node.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true
    });

static ServiceStatusSnapshot BuildStatusSnapshot(RuntimeState runtimeState, ServiceStatusStore statusStore)
{
    var now = DateTimeOffset.UtcNow;
    var latestProcessedAtUtc = runtimeState.ProcessedOrders
        .OrderByDescending(x => x.ProcessedAtUtc)
        .FirstOrDefault()
        ?.ProcessedAtUtc;

    var snapshot = statusStore.GetSnapshot(
        runtimeState.ProcessedOrders.Count,
        latestProcessedAtUtc);

    var pendingBridgeOrders = runtimeState.ProcessedOrders
        .Where(record =>
            string.IsNullOrWhiteSpace(record.BuyerAccountName) &&
            record.ProcessedAtUtc >= now.AddHours(-24))
        .OrderByDescending(record => record.ProcessedAtUtc)
        .ToList();

    snapshot.BridgePendingCount = pendingBridgeOrders.Count;
    snapshot.BridgeMatchedCount = runtimeState.ProcessedOrders.Count(record =>
        !string.IsNullOrWhiteSpace(record.BuyerAccountName));
    snapshot.LatestPendingBridgeOrderAtUtc = pendingBridgeOrders.FirstOrDefault()?.ProcessedAtUtc;
    snapshot.LatestPendingBridgeOrderId = pendingBridgeOrders.FirstOrDefault()?.OrderId ?? string.Empty;
    return snapshot;
}

static object BuildBridgeSignalPayload(ServiceStatusSnapshot snapshot) =>
    new
    {
        serverTimeUtc = DateTimeOffset.UtcNow,
        pendingBridgeCount = snapshot.BridgePendingCount,
        matchedBridgeCount = snapshot.BridgeMatchedCount,
        latestPendingBridgeOrderAtUtc = snapshot.LatestPendingBridgeOrderAtUtc,
        latestPendingBridgeOrderId = snapshot.LatestPendingBridgeOrderId,
        lastBridgeHeartbeatAtUtc = snapshot.LastBridgeHeartbeatAtUtc,
        lastBridgeSourceUrl = snapshot.LastBridgeSourceUrl,
        lastBridgeCaptureAtUtc = snapshot.LastBridgeCaptureAtUtc,
        lastBridgeOrderId = snapshot.LastBridgeOrderId,
        lastBridgeBuyerNickname = snapshot.LastBridgeBuyerNickname
    };

