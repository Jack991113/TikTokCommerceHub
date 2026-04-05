using System.Net;
using Microsoft.Extensions.Options;
using TikTokSalesStats.Models;
using TikTokSalesStats.Options;
using TikTokSalesStats.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<SalesStatsOptions>()
    .Bind(builder.Configuration.GetSection(SalesStatsOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("TikTokStatsApi", client =>
{
    client.DefaultRequestVersion = HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    client.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddSingleton<TikTokRequestSigner>();
builder.Services.AddSingleton<TikTokSalesApiClient>();
builder.Services.AddSingleton<LinkAttributionRuleStore>();
builder.Services.AddSingleton<SalesAnalyticsService>();
builder.Services.AddSingleton<SalesWorkbookExporter>();
builder.Services.AddSingleton<ProductPerformanceWorkbookExporter>();
builder.Services.AddSingleton<StreamerCompensationWorkbookExporter>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/stores", (IOptions<SalesStatsOptions> options) =>
{
    var stores = options.Value.Stores
        .Select(store => new
        {
            store.Key,
            store.Name
        });

    return Results.Json(new
    {
        timezone = options.Value.Timezone,
        stores
    });
});

app.MapGet("/api/product-performance/config", (
    IOptions<SalesStatsOptions> options,
    SalesAnalyticsService analyticsService) =>
{
    var stores = options.Value.Stores
        .Select(store => new
        {
            store.Key,
            store.Name
        });

    return Results.Json(new
    {
        timezone = options.Value.Timezone,
        stores,
        trackedProducts = analyticsService.GetTrackedProducts()
    });
});

app.MapGet("/api/streamer-compensation/config", (
    IOptions<SalesStatsOptions> options,
    SalesAnalyticsService analyticsService) =>
{
    var stores = options.Value.Stores
        .Select(store => new
        {
            store.Key,
            store.Name
        });

    return Results.Json(new
    {
        timezone = options.Value.Timezone,
        stores,
        streamers = analyticsService.GetStreamerCompensationRules()
    });
});

app.MapGet("/api/link-attribution/rules", async (
    LinkAttributionRuleStore ruleStore,
    CancellationToken cancellationToken) =>
{
    var rules = await ruleStore.GetRulesAsync(cancellationToken);
    return Results.Json(new LinkAttributionRulesResponse
    {
        Rules = rules.ToList()
    });
});

app.MapPut("/api/link-attribution/rules", async (
    LinkAttributionRulesResponse payload,
    LinkAttributionRuleStore ruleStore,
    CancellationToken cancellationToken) =>
{
    var rules = await ruleStore.SaveRulesAsync(payload.Rules, cancellationToken);
    return Results.Json(new LinkAttributionRulesResponse
    {
        Rules = rules.ToList()
    });
});

app.MapGet("/api/sales/summary", async (
    string? store,
    string? productIds,
    DateOnly? fromDate,
    DateOnly? toDate,
    bool? includeFullOrderLists,
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder,
    SalesAnalyticsService analyticsService,
    CancellationToken cancellationToken) =>
{
    var summary = await analyticsService.BuildSummaryAsync(
        string.IsNullOrWhiteSpace(store) ? "all" : store,
        fromDate,
        toDate,
        BuildSettings(
            includeTikTokDiscount,
            includeBuyerShippingFee,
            deductPlatformFee,
            deductLogisticsCost,
            platformFeeRate,
            logisticsCostPerOrder),
        SplitValues(productIds),
        includeFullOrderLists is true,
        cancellationToken);

    return Results.Json(summary);
});

app.MapGet("/api/sales/export.xlsx", async (
    string? store,
    string? productIds,
    DateOnly? fromDate,
    DateOnly? toDate,
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder,
    SalesAnalyticsService analyticsService,
    SalesWorkbookExporter workbookExporter,
    CancellationToken cancellationToken) =>
{
    var summary = await analyticsService.BuildSummaryAsync(
        string.IsNullOrWhiteSpace(store) ? "all" : store,
        fromDate,
        toDate,
        BuildSettings(
            includeTikTokDiscount,
            includeBuyerShippingFee,
            deductPlatformFee,
            deductLogisticsCost,
            platformFeeRate,
            logisticsCostPerOrder),
        SplitValues(productIds),
        true,
        cancellationToken);

    var bytes = workbookExporter.BuildWorkbook(summary);
    var safeStore = string.IsNullOrWhiteSpace(summary.StoreKey) ? "all" : summary.StoreKey;
    var fileName = $"sales-dashboard-{safeStore}-{summary.FromUtc:yyyyMMdd}-{summary.ToUtc:yyyyMMdd}.xlsx";

    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
});

app.MapGet("/api/product-performance/summary", async (
    string? store,
    string? productIds,
    DateOnly? fromDate,
    DateOnly? toDate,
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder,
    SalesAnalyticsService analyticsService,
    CancellationToken cancellationToken) =>
{
    var summary = await analyticsService.BuildProductPerformanceAsync(
        string.IsNullOrWhiteSpace(store) ? "all" : store,
        fromDate,
        toDate,
        BuildSettings(
            includeTikTokDiscount,
            includeBuyerShippingFee,
            deductPlatformFee,
            deductLogisticsCost,
            platformFeeRate,
            logisticsCostPerOrder),
        SplitValues(productIds),
        cancellationToken);

    return Results.Json(summary);
});

app.MapGet("/api/product-performance/export.xlsx", async (
    string? store,
    string? productIds,
    DateOnly? fromDate,
    DateOnly? toDate,
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder,
    SalesAnalyticsService analyticsService,
    ProductPerformanceWorkbookExporter workbookExporter,
    CancellationToken cancellationToken) =>
{
    var summary = await analyticsService.BuildProductPerformanceAsync(
        string.IsNullOrWhiteSpace(store) ? "all" : store,
        fromDate,
        toDate,
        BuildSettings(
            includeTikTokDiscount,
            includeBuyerShippingFee,
            deductPlatformFee,
            deductLogisticsCost,
            platformFeeRate,
            logisticsCostPerOrder),
        SplitValues(productIds),
        cancellationToken);

    var bytes = workbookExporter.BuildWorkbook(summary);
    var safeStore = string.IsNullOrWhiteSpace(summary.StoreKey) ? "all" : summary.StoreKey;
    var fileName = $"product-performance-{safeStore}-{summary.FromUtc:yyyyMMdd}-{summary.ToUtc:yyyyMMdd}.xlsx";

    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
});

app.MapGet("/api/streamer-compensation/summary", async (
    string? store,
    DateOnly? fromDate,
    DateOnly? toDate,
    string? ruleOverrides,
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder,
    decimal? cnyToJpyRate,
    decimal? hiddenProcurementCostJpy,
    SalesAnalyticsService analyticsService,
    CancellationToken cancellationToken) =>
{
    var summary = await analyticsService.BuildStreamerCompensationAsync(
        string.IsNullOrWhiteSpace(store) ? "all" : store,
        fromDate,
        toDate,
        BuildSettings(
            includeTikTokDiscount,
            includeBuyerShippingFee,
            deductPlatformFee,
            deductLogisticsCost,
            platformFeeRate,
            logisticsCostPerOrder),
        ParseStreamerOverrides(ruleOverrides),
        cnyToJpyRate,
        hiddenProcurementCostJpy,
        cancellationToken);

    return Results.Json(summary);
});

app.MapGet("/api/streamer-compensation/export.xlsx", async (
    string? store,
    DateOnly? fromDate,
    DateOnly? toDate,
    string? ruleOverrides,
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder,
    decimal? cnyToJpyRate,
    decimal? hiddenProcurementCostJpy,
    SalesAnalyticsService analyticsService,
    StreamerCompensationWorkbookExporter workbookExporter,
    CancellationToken cancellationToken) =>
{
    var summary = await analyticsService.BuildStreamerCompensationAsync(
        string.IsNullOrWhiteSpace(store) ? "all" : store,
        fromDate,
        toDate,
        BuildSettings(
            includeTikTokDiscount,
            includeBuyerShippingFee,
            deductPlatformFee,
            deductLogisticsCost,
            platformFeeRate,
            logisticsCostPerOrder),
        ParseStreamerOverrides(ruleOverrides),
        cnyToJpyRate,
        hiddenProcurementCostJpy,
        cancellationToken);

    var bytes = workbookExporter.BuildWorkbook(summary);
    var safeStore = string.IsNullOrWhiteSpace(summary.StoreKey) ? "all" : summary.StoreKey;
    var fileName = $"streamer-compensation-{safeStore}-{summary.FromUtc:yyyyMMdd}-{summary.ToUtc:yyyyMMdd}.xlsx";

    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

app.Run();

static AnalyticsSettings BuildSettings(
    bool? includeTikTokDiscount,
    bool? includeBuyerShippingFee,
    bool? deductPlatformFee,
    bool? deductLogisticsCost,
    decimal? platformFeeRate,
    decimal? logisticsCostPerOrder) =>
    new()
    {
        IncludeTikTokDiscount = includeTikTokDiscount ?? true,
        IncludeBuyerShippingFee = includeBuyerShippingFee ?? false,
        DeductPlatformFee = deductPlatformFee ?? false,
        DeductLogisticsCost = deductLogisticsCost ?? false,
        PlatformFeeRate = Math.Max(0m, platformFeeRate ?? 0m),
        LogisticsCostPerOrder = Math.Max(0m, logisticsCostPerOrder ?? 0m)
    };

static IReadOnlyList<string> SplitValues(string? values) =>
    string.IsNullOrWhiteSpace(values)
        ? []
        : values
            .Split(new[] { ',', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

static IReadOnlyList<StreamerCompensationOverride> ParseStreamerOverrides(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return [];
    }

    try
    {
        return System.Text.Json.JsonSerializer.Deserialize<List<StreamerCompensationOverride>>(
            value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];
    }
    catch
    {
        return [];
    }
}
