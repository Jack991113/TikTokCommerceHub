using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TikTokSalesStats.Models;
using TikTokSalesStats.Options;

namespace TikTokSalesStats.Services;

public sealed class SalesAnalyticsService
{
    private const string ExcludedUnpaid = "unpaid";
    private const string ExcludedCancelled = "cancelled";
    private const string ExcludedRefunded = "refunded_or_closed";
    private const string UnattributedKey = "unattributed";
    private const string UnattributedLabel = "未归因";
    private const string MixedAttributionKey = "mixed";
    private const string MixedAttributionLabel = "多链接混合";

    private const int DefaultOrderListLimit = 80;
    private static readonly TimeSpan LoadOrdersCacheTtl = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SummaryCacheTtl = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex LongNumberRegex = new(@"\d{8,}", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> TimeZoneFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Tokyo"] = "Tokyo Standard Time",
        ["Tokyo Standard Time"] = "Asia/Tokyo",
        ["Asia/Shanghai"] = "China Standard Time",
        ["China Standard Time"] = "Asia/Shanghai"
    };

    private readonly SalesStatsOptions _options;
    private readonly TikTokSalesApiClient _apiClient;
    private readonly LinkAttributionRuleStore _linkRuleStore;
    private readonly IMemoryCache _cache;

    public SalesAnalyticsService(
        IOptions<SalesStatsOptions> options,
        TikTokSalesApiClient apiClient,
        LinkAttributionRuleStore linkRuleStore,
        IMemoryCache cache)
    {
        _options = options.Value;
        _apiClient = apiClient;
        _linkRuleStore = linkRuleStore;
        _cache = cache;
    }

    public async Task<SalesSummaryResponse> BuildSummaryAsync(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<string>? selectedProductIds,
        bool includeFullOrderLists,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildSummaryCacheKey(storeKey, fromDate, toDate, settings, selectedProductIds, includeFullOrderLists);
        if (_cache.TryGetValue(cacheKey, out SalesSummaryResponse? cached) && cached is not null)
        {
            return cached;
        }

        var summary = await BuildSummaryCoreAsync(
            storeKey,
            fromDate,
            toDate,
            settings,
            selectedProductIds,
            includeFullOrderLists,
            cancellationToken);

        _cache.Set(cacheKey, summary, SummaryCacheTtl);
        return summary;
    }

    private async Task<SalesSummaryResponse> BuildSummaryCoreAsync(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<string>? selectedProductIds,
        bool includeFullOrderLists,
        CancellationToken cancellationToken)
    {
        var effectiveSettings = settings ?? new AnalyticsSettings();
        var timezone = ResolveTimeZone();
        var range = ResolveRange(timezone, fromDate, toDate);
        var normalizedSelectedProductIds = NormalizeProductIds(selectedProductIds);
        var linkRules = await _linkRuleStore.GetRulesAsync(cancellationToken);
        var loadResult = await LoadOrdersCachedAsync(range.FromUtc, range.ToUtc, linkRules, cancellationToken);

        var filteredOrders = string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase)
            ? loadResult.Orders
            : loadResult.Orders.Where(order => string.Equals(order.StoreKey, storeKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var rangedOrders = filteredOrders
            .Where(order => order.RangeAnchorUtc is not null)
            .Where(order => order.RangeAnchorUtc >= range.FromUtc && order.RangeAnchorUtc <= range.ToUtc)
            .OrderByDescending(order => order.PaidAtUtc ?? order.CreatedAtUtc ?? order.UpdatedAtUtc)
            .ThenByDescending(order => order.OrderId)
            .ToList();

        var included = rangedOrders.Where(order => order.IncludedInSales).ToList();
        var excluded = rangedOrders.Where(order => !order.IncludedInSales).ToList();
        var currency = included.Select(order => order.Currency)
            .Concat(excluded.Select(order => order.Currency))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "JPY";

        var overview = BuildOverview(included, excluded, effectiveSettings);
        var productIdBreakdown = BuildProductIdBreakdown(included, effectiveSettings);
        var includedOrderLines = included.Select(order => MapOrderLine(order, effectiveSettings, timezone)).ToList();
        var excludedOrderLines = excluded.Select(order => MapOrderLine(order, effectiveSettings, timezone)).ToList();
        var limitedIncludedOrders = includeFullOrderLists
            ? includedOrderLines
            : includedOrderLines.Take(DefaultOrderListLimit).ToList();
        var limitedExcludedOrders = includeFullOrderLists
            ? excludedOrderLines
            : excludedOrderLines.Take(DefaultOrderListLimit).ToList();

        return new SalesSummaryResponse
        {
            StoreKey = string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase) ? "all" : storeKey,
            StoreName = ResolveStoreLabel(storeKey),
            Timezone = timezone.Id,
            Currency = currency,
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Settings = effectiveSettings,
            Overview = overview,
            Funnel = BuildFunnel(included, excluded),
            BusinessCompass = BuildBusinessCompass(included, overview, range),
            Insights = BuildInsights(included, effectiveSettings, currency),
            Reconciliation = BuildReconciliation(included, effectiveSettings),
            Monthly = BuildMonthly(included, effectiveSettings),
            Daily = BuildDaily(included, effectiveSettings),
            Hourly = BuildHourly(included),
            StoreBreakdown = BuildStoreBreakdown(included, effectiveSettings),
            PaymentBreakdown = BuildPaymentBreakdown(included, effectiveSettings),
            StatusBreakdown = BuildStatusBreakdown(rangedOrders),
            TopBuyers = BuildTopBuyers(included, effectiveSettings),
            PaidBuyerRanking = BuildTopBuyers(included, effectiveSettings),
            TopProducts = BuildTopProducts(included, effectiveSettings),
            SelectedProductIds = normalizedSelectedProductIds.ToList(),
            ProductIdBreakdown = ApplySelectedProductOrder(productIdBreakdown, normalizedSelectedProductIds),
            LinkAttributionBreakdown = BuildLinkAttributionBreakdown(included, effectiveSettings),
            UnpaidReminders = BuildUnpaidReminders(rangedOrders, timezone, loadResult.HistoryLookup),
            PotentialCustomers = BuildPotentialCustomers(rangedOrders, timezone, loadResult.HistoryLookup),
            BlacklistCandidates = BuildBlacklistCandidates(rangedOrders, timezone, loadResult.HistoryLookup),
            DerivedMetrics = BuildSummaryDerivedMetrics(includedOrderLines),
            IncludedOrderTotalCount = includedOrderLines.Count,
            ExcludedOrderTotalCount = excludedOrderLines.Count,
            IncludedOrdersTruncated = !includeFullOrderLists && includedOrderLines.Count > DefaultOrderListLimit,
            ExcludedOrdersTruncated = !includeFullOrderLists && excludedOrderLines.Count > DefaultOrderListLimit,
            TopOrders = included
                .OrderByDescending(order => BuildDisplayedGross(order, effectiveSettings))
                .ThenByDescending(order => order.PaidAtUtc ?? order.CreatedAtUtc ?? order.UpdatedAtUtc)
                .Take(20)
                .Select(order => MapOrderLine(order, effectiveSettings, timezone))
                .ToList(),
            IncludedOrders = limitedIncludedOrders,
            ExcludedOrders = limitedExcludedOrders
        };
    }

    public IReadOnlyList<TrackedProductDefinition> GetTrackedProducts() =>
        _options.TrackedProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductId))
            .Select(item => new TrackedProductDefinition
            {
                ProductId = item.ProductId.Trim(),
                Label = string.IsNullOrWhiteSpace(item.Label) ? item.ProductId.Trim() : item.Label.Trim()
            })
            .DistinctBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<StreamerRuleDefinition> GetStreamerCompensationRules()
    {
        var trackedLookup = GetTrackedProducts()
            .ToDictionary(item => item.ProductId, item => item, StringComparer.OrdinalIgnoreCase);

        return _options.StreamerCompensationRules
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .Select(item => new StreamerRuleDefinition
            {
                Key = item.Key.Trim(),
                Label = string.IsNullOrWhiteSpace(item.Label) ? item.Key.Trim() : item.Label.Trim(),
                BaseSalaryAmount = Math.Max(0m, item.BaseSalaryAmount),
                BaseSalaryCurrency = NormalizeSalaryCurrency(item.BaseSalaryCurrency),
                CommissionRate = Math.Max(0m, item.CommissionRate),
                CommissionLabel = string.IsNullOrWhiteSpace(item.CommissionLabel) ? $"{Math.Max(0m, item.CommissionRate) * 100m:0.##}%" : item.CommissionLabel.Trim(),
                Note = item.Note?.Trim() ?? string.Empty,
                ProductIds = item.ProductIds
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(item => item.ProductIds.Count > 0)
            .Select(item =>
            {
                item.ProductIds = item.ProductIds
                    .Select(productId => trackedLookup.TryGetValue(productId, out var tracked) ? tracked.ProductId : productId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return item;
            })
            .ToList();
    }

    public IReadOnlyList<StreamerRuleDefinition> ApplyStreamerCompensationOverrides(
        IReadOnlyCollection<StreamerCompensationOverride>? overrides)
    {
        var baseRules = GetStreamerCompensationRules();
        if (overrides is not { Count: > 0 })
        {
            return baseRules;
        }

        var baseRuleLookup = baseRules.ToDictionary(rule => rule.Key, StringComparer.OrdinalIgnoreCase);
        var mergedRules = new List<StreamerRuleDefinition>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in overrides.Where(item => !string.IsNullOrWhiteSpace(item.Key)))
        {
            var normalizedKey = item.Key.Trim();
            if (!seenKeys.Add(normalizedKey))
            {
                continue;
            }

            baseRuleLookup.TryGetValue(normalizedKey, out var baseRule);

            var label = string.IsNullOrWhiteSpace(item.Label)
                ? baseRule?.Label ?? normalizedKey
                : item.Label.Trim();
            var note = string.IsNullOrWhiteSpace(item.Note)
                ? baseRule?.Note ?? string.Empty
                : item.Note.Trim();
            var productIds = NormalizeProductIds(item.ProductIds.Count > 0 ? item.ProductIds : baseRule?.ProductIds);
            var baseSalaryAmount = item.BaseSalaryAmount is >= 0m
                ? item.BaseSalaryAmount.Value
                : baseRule?.BaseSalaryAmount ?? 0m;
            var commissionRate = item.CommissionRate is >= 0m
                ? item.CommissionRate.Value
                : baseRule?.CommissionRate ?? 0m;

            mergedRules.Add(new StreamerRuleDefinition
            {
                Key = normalizedKey,
                Label = label,
                Note = note,
                ProductIds = productIds,
                BaseSalaryCurrency = "RMB",
                BaseSalaryAmount = Math.Max(0m, baseSalaryAmount),
                CommissionRate = Math.Max(0m, commissionRate),
                CommissionLabel = $"{Math.Max(0m, commissionRate) * 100m:0.##}%"
            });
        }

        mergedRules.AddRange(baseRules.Where(rule => !seenKeys.Contains(rule.Key)));
        return mergedRules;
    }

    public async Task<ProductPerformanceResponse> BuildProductPerformanceAsync(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<string>? selectedProductIds,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildProductPerformanceCacheKey(storeKey, fromDate, toDate, settings, selectedProductIds);
        if (_cache.TryGetValue(cacheKey, out ProductPerformanceResponse? cached) && cached is not null)
        {
            return cached;
        }

        var effectiveSettings = settings ?? new AnalyticsSettings();
        var timezone = ResolveTimeZone();
        var range = ResolveRange(timezone, fromDate, toDate);
        var configuredProducts = GetTrackedProducts();
        var normalizedSelectedProductIds = NormalizeProductIds(
            selectedProductIds is { Count: > 0 }
                ? selectedProductIds
                : configuredProducts.Select(item => item.ProductId).ToList());
        var selectedProductSet = normalizedSelectedProductIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trackedDefinitions = configuredProducts
            .Where(item => selectedProductSet.Contains(item.ProductId))
            .Concat(normalizedSelectedProductIds
                .Where(id => configuredProducts.All(item => !string.Equals(item.ProductId, id, StringComparison.OrdinalIgnoreCase)))
                .Select(id => new TrackedProductDefinition
                {
                    ProductId = id,
                    Label = id
                }))
            .DistinctBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linkRules = await _linkRuleStore.GetRulesAsync(cancellationToken);
        var loadResult = await LoadOrdersCachedAsync(range.FromUtc, range.ToUtc, linkRules, cancellationToken);

        var filteredOrders = string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase)
            ? loadResult.Orders
            : loadResult.Orders.Where(order => string.Equals(order.StoreKey, storeKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var included = filteredOrders
            .Where(order => order.IncludedInSales)
            .Where(order => order.RangeAnchorUtc is not null)
            .Where(order => order.RangeAnchorUtc >= range.FromUtc && order.RangeAnchorUtc <= range.ToUtc)
            .OrderByDescending(order => order.PaidAtUtc ?? order.CreatedAtUtc ?? order.UpdatedAtUtc)
            .ThenByDescending(order => order.OrderId)
            .ToList();

        var currency = included
            .Select(order => order.Currency)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "JPY";

        var products = BuildTrackedProductPerformance(included, effectiveSettings, trackedDefinitions);
        var totals = BuildTrackedProductTotals(products);

        var response = new ProductPerformanceResponse
        {
            StoreKey = string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase) ? "all" : storeKey,
            StoreName = ResolveStoreLabel(storeKey),
            Timezone = timezone.Id,
            Currency = currency,
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Settings = effectiveSettings,
            Totals = totals,
            TrackedProducts = trackedDefinitions,
            Products = products
        };

        _cache.Set(cacheKey, response, SummaryCacheTtl);
        return response;
    }

    public async Task<StreamerCompensationResponse> BuildStreamerCompensationAsync(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<StreamerCompensationOverride>? overrides,
        decimal? cnyToJpyRate,
        decimal? hiddenProcurementCostJpy,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildStreamerCompensationCacheKey(storeKey, fromDate, toDate, settings, overrides, cnyToJpyRate, hiddenProcurementCostJpy);
        if (_cache.TryGetValue(cacheKey, out StreamerCompensationResponse? cached) && cached is not null)
        {
            return cached;
        }

        var effectiveSettings = settings ?? new AnalyticsSettings();
        var timezone = ResolveTimeZone();
        var range = ResolveRange(timezone, fromDate, toDate);
        var linkRules = await _linkRuleStore.GetRulesAsync(cancellationToken);
        var loadResult = await LoadOrdersCachedAsync(range.FromUtc, range.ToUtc, linkRules, cancellationToken);

        var filteredOrders = string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase)
            ? loadResult.Orders
            : loadResult.Orders.Where(order => string.Equals(order.StoreKey, storeKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var included = filteredOrders
            .Where(order => order.IncludedInSales)
            .Where(order => order.RangeAnchorUtc is not null)
            .Where(order => order.RangeAnchorUtc >= range.FromUtc && order.RangeAnchorUtc <= range.ToUtc)
            .OrderByDescending(order => order.PaidAtUtc ?? order.CreatedAtUtc ?? order.UpdatedAtUtc)
            .ThenByDescending(order => order.OrderId)
            .ToList();

        var currency = included
            .Select(order => order.Currency)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "JPY";

        var trackedProducts = GetTrackedProducts();
        var trackedLookup = trackedProducts
            .ToDictionary(item => item.ProductId, item => item, StringComparer.OrdinalIgnoreCase);
        var streamerRules = ApplyStreamerCompensationOverrides(overrides);
        var assignedProductIds = streamerRules
            .SelectMany(item => item.ProductIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selfOwnedProducts = trackedProducts
            .Where(item => !assignedProductIds.Contains(item.ProductId))
            .ToList();

        var allRelevantDefinitions = streamerRules
            .SelectMany(item => item.ProductIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(productId => trackedLookup.TryGetValue(productId, out var tracked) ? tracked : new TrackedProductDefinition { ProductId = productId, Label = productId })
            .Concat(selfOwnedProducts)
            .DistinctBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var definitionLookup = allRelevantDefinitions
            .ToDictionary(item => item.ProductId, item => item, StringComparer.OrdinalIgnoreCase);
        var productLines = BuildProductPerformanceLines(included, effectiveSettings, definitionLookup);
        var localFrom = TimeZoneInfo.ConvertTime(range.FromUtc, timezone);
        var localTo = TimeZoneInfo.ConvertTime(range.ToUtc, timezone);
        var months = BuildMonthSequence(DateOnly.FromDateTime(localFrom.DateTime), DateOnly.FromDateTime(localTo.DateTime));
        var effectiveCnyToJpyRate = cnyToJpyRate is > 0m ? cnyToJpyRate.Value : 20m;
        var effectiveHiddenProcurementCostJpy = Math.Max(0m, hiddenProcurementCostJpy ?? 0m);

        var streamerSummaries = streamerRules
            .Select(rule => BuildStreamerCompensationSummary(
                rule,
                productLines.Where(line => rule.ProductIds.Contains(line.ProductId, StringComparer.OrdinalIgnoreCase)).ToList(),
                months,
                effectiveCnyToJpyRate,
                currency))
            .ToList();

        var selfOwnedSummary = BuildSelfOwnedCompensationSummary(
            productLines.Where(line => selfOwnedProducts.Any(product => string.Equals(product.ProductId, line.ProductId, StringComparison.OrdinalIgnoreCase))).ToList(),
            selfOwnedProducts,
            months,
            currency);

        var totalPaidForAllocation = streamerSummaries.Sum(item => item.PaidAmount) + selfOwnedSummary.PaidAmount;
        foreach (var summary in streamerSummaries)
        {
            summary.AllocatedHiddenProcurementCostJpy = totalPaidForAllocation <= 0m
                ? 0m
                : Math.Round(effectiveHiddenProcurementCostJpy * (summary.PaidAmount / totalPaidForAllocation), 2);
            summary.ProfitAfterHiddenCostJpy = summary.ProfitBeforeHiddenCostJpy - summary.AllocatedHiddenProcurementCostJpy;
            summary.ProfitAfterHiddenCostWithEstimatedLogisticsJpy = summary.ProfitBeforeHiddenCostWithEstimatedLogisticsJpy - summary.AllocatedHiddenProcurementCostJpy;
            summary.ProfitAfterHiddenCostWithCalculatedShippingJpy = summary.ProfitBeforeHiddenCostWithCalculatedShippingJpy - summary.AllocatedHiddenProcurementCostJpy;
        }

        selfOwnedSummary.AllocatedHiddenProcurementCostJpy = totalPaidForAllocation <= 0m
            ? 0m
            : Math.Round(effectiveHiddenProcurementCostJpy * (selfOwnedSummary.PaidAmount / totalPaidForAllocation), 2);
        selfOwnedSummary.ProfitAfterHiddenCostJpy = selfOwnedSummary.ProfitBeforeHiddenCostJpy - selfOwnedSummary.AllocatedHiddenProcurementCostJpy;
        selfOwnedSummary.ProfitAfterHiddenCostWithEstimatedLogisticsJpy = selfOwnedSummary.ProfitBeforeHiddenCostWithEstimatedLogisticsJpy - selfOwnedSummary.AllocatedHiddenProcurementCostJpy;
        selfOwnedSummary.ProfitAfterHiddenCostWithCalculatedShippingJpy = selfOwnedSummary.ProfitBeforeHiddenCostWithCalculatedShippingJpy - selfOwnedSummary.AllocatedHiddenProcurementCostJpy;

        var monthlyProfit = BuildStreamerMonthlyProfit(streamerSummaries, selfOwnedSummary, months, effectiveHiddenProcurementCostJpy, totalPaidForAllocation);
        var totals = BuildStreamerCompensationTotals(streamerSummaries, selfOwnedSummary, monthlyProfit, effectiveHiddenProcurementCostJpy, productLines);

        var response = new StreamerCompensationResponse
        {
            StoreKey = string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase) ? "all" : storeKey,
            StoreName = ResolveStoreLabel(storeKey),
            Timezone = timezone.Id,
            Currency = currency,
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Settings = effectiveSettings,
            CnyToJpyRate = effectiveCnyToJpyRate,
            HiddenProcurementCostJpy = effectiveHiddenProcurementCostJpy,
            Totals = totals,
            Streamers = streamerSummaries,
            SelfOwned = selfOwnedSummary,
            MonthlyProfit = monthlyProfit
        };

        _cache.Set(cacheKey, response, SummaryCacheTtl);
        return response;
    }

    private async Task<LoadOrdersResult> LoadOrdersCachedAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyList<LinkAttributionRuleRecord> linkRules,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildLoadOrdersCacheKey(fromUtc, toUtc, linkRules);
        if (_cache.TryGetValue(cacheKey, out LoadOrdersResult? cached) && cached is not null)
        {
            return cached;
        }

        var result = await LoadOrdersAsync(fromUtc, toUtc, linkRules, cancellationToken);
        _cache.Set(cacheKey, result, LoadOrdersCacheTtl);
        return result;
    }

    private async Task<LoadOrdersResult> LoadOrdersAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyList<LinkAttributionRuleRecord> linkRules,
        CancellationToken cancellationToken)
    {
        var orders = new List<SalesOrderRecord>();
        var historyLookup = new Dictionary<string, CustomerHistoryRecord>(StringComparer.OrdinalIgnoreCase);
        var timezone = ResolveTimeZone();

        foreach (var store in _options.Stores)
        {
            if (string.IsNullOrWhiteSpace(store.RuntimeStatePath) || !File.Exists(store.RuntimeStatePath))
            {
                continue;
            }

            var runtimeState = await LoadRuntimeStateAsync(store.RuntimeStatePath, cancellationToken);
            var handleLookup = BuildHandleLookup(runtimeState.ProcessedOrders);
            MergeHistoricalOrders(store.Key, runtimeState.ProcessedOrders, historyLookup);

            var apiOrders = await _apiClient.SearchOrdersAsync(runtimeState, fromUtc, toUtc, cancellationToken);
            foreach (var orderNode in apiOrders)
            {
                var order = BuildOrderRecord(store, runtimeState.StoreName, orderNode, handleLookup, linkRules, timezone);
                orders.Add(order);
                MergeHistoricalOrder(store.Key, order, historyLookup);
            }
        }

        return new LoadOrdersResult(orders, historyLookup);
    }

    private static string BuildLoadOrdersCacheKey(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyList<LinkAttributionRuleRecord> linkRules)
    {
        var stamp = string.Join("|",
            linkRules
                .OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
                .Select(rule => $"{rule.Id}:{rule.Label}:{rule.Enabled}:{string.Join(',', rule.StoreKeys.OrderBy(x => x))}:{string.Join(',', rule.ProductIds.OrderBy(x => x))}:{string.Join(',', rule.SkuIds.OrderBy(x => x))}:{string.Join(',', rule.ProductNameKeywords.OrderBy(x => x))}"));
        return $"load-orders:{fromUtc:O}:{toUtc:O}:{stamp}";
    }

    private static string BuildSummaryCacheKey(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<string>? selectedProductIds,
        bool includeFullOrderLists)
    {
        var effective = settings ?? new AnalyticsSettings();
        var products = string.Join(",", NormalizeProductIds(selectedProductIds));
        return $"sales-summary:{storeKey}:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}:{effective.IncludeTikTokDiscount}:{effective.IncludeBuyerShippingFee}:{effective.DeductPlatformFee}:{effective.DeductLogisticsCost}:{effective.PlatformFeeRate}:{effective.LogisticsCostPerOrder}:{includeFullOrderLists}:{products}";
    }

    private static string BuildProductPerformanceCacheKey(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<string>? selectedProductIds)
    {
        var effective = settings ?? new AnalyticsSettings();
        var products = string.Join(",", NormalizeProductIds(selectedProductIds));
        return $"product-performance:{storeKey}:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}:{effective.IncludeTikTokDiscount}:{effective.IncludeBuyerShippingFee}:{effective.DeductPlatformFee}:{effective.DeductLogisticsCost}:{effective.PlatformFeeRate}:{effective.LogisticsCostPerOrder}:{products}";
    }

    private static string BuildStreamerCompensationCacheKey(
        string storeKey,
        DateOnly? fromDate,
        DateOnly? toDate,
        AnalyticsSettings? settings,
        IReadOnlyCollection<StreamerCompensationOverride>? overrides,
        decimal? cnyToJpyRate,
        decimal? hiddenProcurementCostJpy)
    {
        var effective = settings ?? new AnalyticsSettings();
        var overrideStamp = overrides is { Count: > 0 }
            ? string.Join("|", overrides
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}:{item.Label}:{item.BaseSalaryAmount}:{item.CommissionRate}:{string.Join(',', NormalizeProductIds(item.ProductIds))}"))
            : "none";
        return $"streamer-summary:{storeKey}:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}:{effective.IncludeTikTokDiscount}:{effective.IncludeBuyerShippingFee}:{effective.DeductPlatformFee}:{effective.DeductLogisticsCost}:{effective.PlatformFeeRate}:{effective.LogisticsCostPerOrder}:{cnyToJpyRate}:{hiddenProcurementCostJpy}:{overrideStamp}";
    }

    private async Task<RuntimeStateSnapshot> LoadRuntimeStateAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<RuntimeStateSnapshot>(json, JsonOptions) ?? new RuntimeStateSnapshot();
    }

    private static SummaryDerivedMetrics BuildSummaryDerivedMetrics(IReadOnlyList<OrderLineSummary> includedOrders)
    {
        return new SummaryDerivedMetrics
        {
            OrderBuckets = BuildOrderBuckets(includedOrders),
            HandleSources = BuildHandleSources(includedOrders),
            BuyerSegments = BuildBuyerSegments(includedOrders),
            SettlementRows = BuildSettlementRows(includedOrders),
            DiscountOrders = includedOrders
                .Where(item => item.TikTokDiscountAmount > 0m)
                .OrderByDescending(item => item.TikTokDiscountAmount)
                .ThenByDescending(item => item.PaidAmount)
                .Take(12)
                .ToList()
        };
    }

    private static List<OrderBucketSummary> BuildOrderBuckets(IReadOnlyList<OrderLineSummary> orders)
    {
        var buckets = new List<OrderBucketSummary>
        {
            new() { Label = "0-999" },
            new() { Label = "1000-2999" },
            new() { Label = "3000-4999" },
            new() { Label = "5000-9999" },
            new() { Label = "10000+" }
        };

        foreach (var order in orders)
        {
            var amount = order.GrossWithDiscount;
            var bucket = amount switch
            {
                <= 999.99m => buckets[0],
                <= 2999.99m => buckets[1],
                <= 4999.99m => buckets[2],
                <= 9999.99m => buckets[3],
                _ => buckets[4]
            };

            bucket.OrderCount += 1;
            bucket.GrossWithDiscount += amount;
        }

        return buckets;
    }

    private static List<HandleSourceSummary> BuildHandleSources(IReadOnlyList<OrderLineSummary> orders)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["local_bridge"] = "本地桥接",
            ["api"] = "API 返回",
            ["api_only"] = "仅订单接口",
            ["unknown"] = "未识别"
        };

        return orders
            .GroupBy(order => string.IsNullOrWhiteSpace(order.BuyerHandleSource) ? "unknown" : order.BuyerHandleSource, StringComparer.OrdinalIgnoreCase)
            .Select(group => new HandleSourceSummary
            {
                Label = labels.TryGetValue(group.Key, out var label) ? label : group.Key,
                OrderCount = group.Count(),
                GrossWithDiscount = group.Sum(item => item.GrossWithDiscount)
            })
            .OrderByDescending(item => item.OrderCount)
            .ToList();
    }

    private static List<BuyerSegmentSummary> BuildBuyerSegments(IReadOnlyList<OrderLineSummary> orders)
    {
        var segments = new List<BuyerSegmentSummary>
        {
            new() { Label = "1 单新客" },
            new() { Label = "2-3 单轻复购" },
            new() { Label = "4-6 单高频客" },
            new() { Label = "7 单以上核心客" }
        };

        var buyers = orders
            .Where(order => !string.IsNullOrWhiteSpace(order.BuyerHandle))
            .GroupBy(order => order.BuyerHandle, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                OrderCount = group.Count(),
                GrossWithDiscount = group.Sum(item => item.GrossWithDiscount)
            });

        foreach (var buyer in buyers)
        {
            var segment = buyer.OrderCount switch
            {
                1 => segments[0],
                >= 2 and <= 3 => segments[1],
                >= 4 and <= 6 => segments[2],
                _ => segments[3]
            };

            segment.BuyerCount += 1;
            segment.OrderCount += buyer.OrderCount;
            segment.GrossWithDiscount += buyer.GrossWithDiscount;
        }

        return segments;
    }

    private static List<SettlementRowSummary> BuildSettlementRows(IReadOnlyList<OrderLineSummary> orders)
    {
        var settled = orders.Where(order => (order.SettlementState ?? string.Empty).Contains("已完结", StringComparison.OrdinalIgnoreCase)).ToList();
        var pending = orders.Where(order => !(order.SettlementState ?? string.Empty).Contains("已完结", StringComparison.OrdinalIgnoreCase)).ToList();
        return
        [
            new SettlementRowSummary
            {
                Label = "已回款估算",
                Amount = settled.Sum(item => item.EstimatedReceivableAmount),
                OrderCount = settled.Count
            },
            new SettlementRowSummary
            {
                Label = "待回款估算",
                Amount = pending.Sum(item => item.EstimatedReceivableAmount),
                OrderCount = pending.Count
            }
        ];
    }

    private static Dictionary<string, string> BuildHandleLookup(IEnumerable<ProcessedOrderRecord> processedOrders) =>
        processedOrders
            .Where(order => !string.IsNullOrWhiteSpace(order.OrderId))
            .GroupBy(order => order.OrderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.BuyerAccountNameCapturedAtUtc ?? item.UpdatedAtUtc ?? item.ProcessedAtUtc ?? item.CreatedAtUtc ?? item.PaidAtUtc)
                    .Select(item => item.BuyerAccountName?.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private static void MergeHistoricalOrders(
        string storeKey,
        IEnumerable<ProcessedOrderRecord> processedOrders,
        IDictionary<string, CustomerHistoryRecord> historyLookup)
    {
        foreach (var processed in processedOrders)
        {
            var buyerKey = ResolveBuyerKey(processed.BuyerAccountName, processed.BuyerPlatformUserId, processed.BuyerEmail);
            if (string.IsNullOrWhiteSpace(buyerKey))
            {
                continue;
            }

            var createdAt = processed.CreatedAtUtc ?? processed.ProcessedAtUtc ?? processed.UpdatedAtUtc ?? processed.PaidAtUtc;
            if (createdAt is null)
            {
                continue;
            }

            var historyKey = ComposeHistoryKey(storeKey, buyerKey);
            MergeHistoryRecord(
                historyLookup,
                historyKey,
                new CustomerHistoryRecord(
                    storeKey,
                    processed.OrderId,
                    buyerKey,
                    ResolveBuyerLabel(processed.BuyerAccountName, processed.BuyerPlatformUserId, processed.BuyerEmail),
                    processed.BuyerPlatformUserId,
                    processed.BuyerEmail,
                    processed.Status,
                    createdAt.Value));
        }
    }

    private static void MergeHistoricalOrder(
        string storeKey,
        SalesOrderRecord order,
        IDictionary<string, CustomerHistoryRecord> historyLookup)
    {
        var buyerKey = ResolveBuyerKey(order);
        if (string.IsNullOrWhiteSpace(buyerKey))
        {
            return;
        }

        var createdAt = order.CreatedAtUtc ?? order.UpdatedAtUtc ?? order.PaidAtUtc;
        if (createdAt is null)
        {
            return;
        }

        var historyKey = ComposeHistoryKey(storeKey, buyerKey);
        MergeHistoryRecord(
            historyLookup,
            historyKey,
            new CustomerHistoryRecord(
                storeKey,
                order.OrderId,
                buyerKey,
                ResolveBuyerLabel(order),
                order.BuyerUserId,
                order.BuyerEmail,
                order.Status,
                createdAt.Value));
    }

    private static void MergeHistoryRecord(
        IDictionary<string, CustomerHistoryRecord> historyLookup,
        string historyKey,
        CustomerHistoryRecord candidate)
    {
        if (!historyLookup.TryGetValue(historyKey, out var current) ||
            candidate.CreatedAtUtc < current.CreatedAtUtc ||
            (candidate.CreatedAtUtc == current.CreatedAtUtc &&
             string.CompareOrdinal(candidate.OrderId, current.OrderId) < 0))
        {
            historyLookup[historyKey] = candidate;
        }
    }

    private static SalesOrderRecord BuildOrderRecord(
        StoreDataSourceOptions store,
        string stateStoreName,
        JsonObject orderNode,
        IReadOnlyDictionary<string, string> buyerHandleLookup,
        IReadOnlyList<LinkAttributionRuleRecord> linkRules,
        TimeZoneInfo timezone)
    {
        var orderId = ExtractString(orderNode, "id") ?? string.Empty;
        var paidAtUtc = ParseUnixSeconds(orderNode["paid_time"]);
        var createdAtUtc = ParseUnixSeconds(orderNode["create_time"]);
        var updatedAtUtc = ParseUnixSeconds(orderNode["update_time"]);
        var status = ExtractString(orderNode, "status") ?? string.Empty;
        var payment = orderNode["payment"] as JsonObject;
        var currency = ExtractString(payment, "currency") ?? ExtractString(orderNode, "currency") ?? "JPY";

        var bridgedHandle = buyerHandleLookup.TryGetValue(orderId, out var capturedHandle)
            ? capturedHandle
            : string.Empty;
        var apiHandle = ExtractString(orderNode, "buyer_nickname")
            ?? ExtractString(orderNode["buyer"] as JsonObject, "nickname")
            ?? ExtractString(orderNode, "nickname")
            ?? string.Empty;
        var buyerHandle = !string.IsNullOrWhiteSpace(bridgedHandle) ? bridgedHandle : apiHandle;
        var handleSource = !string.IsNullOrWhiteSpace(bridgedHandle)
            ? "local_bridge"
            : (!string.IsNullOrWhiteSpace(apiHandle) ? "api" : "api_only");

        var paidLocal = paidAtUtc is null ? (DateTimeOffset?)null : TimeZoneInfo.ConvertTime(paidAtUtc.Value, timezone);
        var exclusionReason = ResolveExclusionReason(status, paidAtUtc);
        var items = ParseLineItems(orderNode);
        ApplyLinkAttribution(store.Key, items, linkRules);
        var (linkKey, linkLabel, linkUrl) = ResolveOrderLinkAttribution(items);

        return new SalesOrderRecord
        {
            StoreKey = store.Key,
            StoreName = string.IsNullOrWhiteSpace(stateStoreName) ? store.Name : stateStoreName,
            OrderId = orderId,
            BuyerHandle = buyerHandle,
            BuyerHandleSource = handleSource,
            BuyerUserId = ExtractString(orderNode, "user_id") ?? string.Empty,
            BuyerEmail = ExtractString(orderNode, "buyer_email") ?? string.Empty,
            Status = status,
            Currency = currency,
            PaymentMethod = ExtractString(orderNode, "payment_method_name") ?? "Unknown",
            DeliveryOptionName = ExtractString(orderNode, "delivery_option_name") ?? string.Empty,
            PaidAmount = ParseDecimal(payment?["total_amount"]) ?? 0m,
            TikTokDiscountAmount = SumDecimals(payment?["platform_discount"], payment?["shipping_fee_platform_discount"], payment?["payment_platform_discount"]),
            BuyerShippingFeeAmount = ParseDecimal(payment?["shipping_fee"]) ?? 0m,
            OriginalShippingFeeAmount = ParseDecimal(payment?["original_shipping_fee"]) ?? 0m,
            SellerDiscountAmount = ParseDecimal(payment?["seller_discount"]) ?? 0m,
            ShippingSellerDiscountAmount = ParseDecimal(payment?["shipping_fee_seller_discount"]) ?? 0m,
            ShippingPlatformDiscountAmount = ParseDecimal(payment?["shipping_fee_platform_discount"]) ?? 0m,
            CreatedAtUtc = createdAtUtc,
            PaidAtUtc = paidAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            RangeAnchorUtc = paidAtUtc ?? createdAtUtc ?? updatedAtUtc,
            LocalPaidDate = paidLocal?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            LocalPaidMonth = paidLocal?.ToString("yyyy-MM", CultureInfo.InvariantCulture) ?? string.Empty,
            LocalPaidHour = paidLocal?.Hour,
            IncludedInSales = string.IsNullOrWhiteSpace(exclusionReason),
            ExclusionReason = exclusionReason,
            LinkAttributionKey = linkKey,
            LinkAttributionLabel = linkLabel,
            LinkAttributionUrl = linkUrl,
            Items = items
        };
    }

    private static List<SalesItemRecord> ParseLineItems(JsonObject orderNode)
    {
        if (orderNode["line_items"] is not JsonArray lineItems)
        {
            return [];
        }

        return lineItems
            .OfType<JsonObject>()
            .Select(lineItem => new SalesItemRecord
            {
                ProductId = ExtractString(lineItem, "product_id") ?? string.Empty,
                SkuId = ExtractString(lineItem, "sku_id") ?? string.Empty,
                ProductName = ExtractString(lineItem, "product_name") ?? string.Empty,
                SkuName = ExtractString(lineItem, "sku_name") ?? string.Empty,
                Quantity = ResolveItemQuantity(lineItem),
                SalePrice = ParseDecimal(lineItem["sale_price"]) ?? 0m,
                OriginalPrice = ParseDecimal(lineItem["original_price"]) ?? 0m,
                TikTokDiscountAmount = ParseDecimal(lineItem["platform_discount"]) ?? 0m
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductName) || !string.IsNullOrWhiteSpace(item.SkuName))
            .ToList();
    }

    private static int ResolveItemQuantity(JsonObject lineItem)
    {
        var directQuantity = ParseInt(lineItem["quantity"])
            ?? ParseInt(lineItem["count"])
            ?? ParseInt(lineItem["item_count"])
            ?? ParseInt(lineItem["item_num"]);

        if (directQuantity is > 0)
        {
            return directQuantity.Value;
        }

        if (lineItem["combined_listing_skus"] is JsonArray combinedListingSkus)
        {
            var combinedQuantity = combinedListingSkus
                .OfType<JsonObject>()
                .Sum(node => ParseInt(node["sku_count"]) ?? 0);

            if (combinedQuantity > 0)
            {
                return combinedQuantity;
            }
        }

        return 1;
    }

    private static void ApplyLinkAttribution(
        string storeKey,
        IList<SalesItemRecord> items,
        IReadOnlyList<LinkAttributionRuleRecord> linkRules)
    {
        if (items.Count == 0)
        {
            return;
        }

        var normalizedRules = NormalizeLinkRules(linkRules, storeKey);
        foreach (var item in items)
        {
            var bestMatch = normalizedRules
                .Select(rule => new
                {
                    Rule = rule,
                    Score = ScoreRuleMatch(rule, item)
                })
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Rule.Label, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestMatch is null)
            {
                item.LinkAttributionKey = UnattributedKey;
                item.LinkAttributionLabel = UnattributedLabel;
                item.LinkAttributionUrl = string.Empty;
                continue;
            }

            item.LinkAttributionKey = bestMatch.Rule.Id;
            item.LinkAttributionLabel = bestMatch.Rule.Label;
            item.LinkAttributionUrl = bestMatch.Rule.LinkUrl;
        }
    }

    private static (string Key, string Label, string Url) ResolveOrderLinkAttribution(IReadOnlyCollection<SalesItemRecord> items)
    {
        var linkedItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.LinkAttributionLabel))
            .ToList();

        if (linkedItems.Count == 0)
        {
            return (UnattributedKey, UnattributedLabel, string.Empty);
        }

        var groups = linkedItems
            .GroupBy(item => new { item.LinkAttributionKey, item.LinkAttributionLabel, item.LinkAttributionUrl })
            .OrderByDescending(group => group.Sum(item => item.SalePrice))
            .ThenByDescending(group => group.Count())
            .ToList();

        if (groups.Count == 1)
        {
            var top = groups[0].Key;
            return (top.LinkAttributionKey, top.LinkAttributionLabel, top.LinkAttributionUrl);
        }

        if (groups.Count(group => !string.Equals(group.Key.LinkAttributionKey, UnattributedKey, StringComparison.OrdinalIgnoreCase)) == 1)
        {
            var top = groups
                .First(group => !string.Equals(group.Key.LinkAttributionKey, UnattributedKey, StringComparison.OrdinalIgnoreCase))
                .Key;
            return (top.LinkAttributionKey, top.LinkAttributionLabel, top.LinkAttributionUrl);
        }

        return (MixedAttributionKey, MixedAttributionLabel, string.Empty);
    }

    private static List<NormalizedLinkAttributionRule> NormalizeLinkRules(
        IEnumerable<LinkAttributionRuleRecord> rules,
        string storeKey) =>
        rules
            .Where(rule => rule.Enabled && !string.IsNullOrWhiteSpace(rule.Label))
            .Where(rule =>
                rule.StoreKeys.Count == 0 ||
                rule.StoreKeys.Any(value => string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)) ||
                rule.StoreKeys.Any(value => string.Equals(value, storeKey, StringComparison.OrdinalIgnoreCase)))
            .Select(rule => new NormalizedLinkAttributionRule(
                rule.Id,
                rule.Label,
                rule.LinkUrl,
                ToLookupSet(rule.ProductIds.Concat(ExtractLongNumericTokens(rule.LinkUrl))),
                ToLookupSet(rule.SkuIds.Concat(ExtractLongNumericTokens(rule.LinkUrl))),
                ToLookupSet(rule.ProductNameKeywords)))
            .ToList();

    private static int ScoreRuleMatch(NormalizedLinkAttributionRule rule, SalesItemRecord item)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(item.ProductId) && rule.ProductIds.Contains(item.ProductId.Trim()))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(item.SkuId) && rule.SkuIds.Contains(item.SkuId.Trim()))
        {
            score += 90;
        }

        var searchable = $"{item.ProductName} {item.SkuName} {item.DisplayName}".Trim();
        if (!string.IsNullOrWhiteSpace(searchable))
        {
            foreach (var keyword in rule.ProductNameKeywords)
            {
                if (searchable.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score += 25;
                }
            }
        }

        return score;
    }

    private static HashSet<string> ToLookupSet(IEnumerable<string> values) =>
        values
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ExtractLongNumericTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in LongNumberRegex.Matches(text))
        {
            if (match.Success)
            {
                yield return match.Value;
            }
        }
    }

    private static OverviewSummary BuildOverview(
        IReadOnlyCollection<SalesOrderRecord> included,
        IReadOnlyCollection<SalesOrderRecord> excluded,
        AnalyticsSettings settings)
    {
        var totalOrders = included.Count + excluded.Count;
        var buyerKeys = included
            .Select(ResolveBuyerKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToList();

        var uniqueBuyerCount = buyerKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var repeatBuyerCount = buyerKeys
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Count() > 1);
        var handleCoverageCount = included.Count(order => !string.IsNullOrWhiteSpace(order.BuyerHandle));

        return new OverviewSummary
        {
            ObservedOrderCount = totalOrders,
            IncludedOrderCount = included.Count,
            IncludedPaidAmount = included.Sum(order => order.PaidAmount),
            IncludedTikTokDiscountAmount = included.Sum(order => order.TikTokDiscountAmount),
            IncludedGrossWithDiscount = included.Sum(order => BuildDisplayedGross(order, settings)),
            AverageOrderValue = included.Count == 0 ? 0m : included.Sum(order => order.PaidAmount) / included.Count,
            UniqueBuyerCount = uniqueBuyerCount,
            RepeatBuyerCount = repeatBuyerCount,
            RepeatBuyerRate = uniqueBuyerCount == 0 ? 0m : repeatBuyerCount * 100m / uniqueBuyerCount,
            HandleCoverageRate = included.Count == 0 ? 0m : handleCoverageCount * 100m / included.Count,
            ValidOrderRate = totalOrders == 0 ? 0m : included.Count * 100m / totalOrders,
            ExcludedTotalCount = excluded.Count,
            AwaitingCollectionStatusCount = included.Count(order => string.Equals(order.Status, "AWAITING_COLLECTION", StringComparison.OrdinalIgnoreCase)),
            ExcludedCancelledCount = excluded.Count(order => order.ExclusionReason == ExcludedCancelled),
            ExcludedUnpaidCount = excluded.Count(order => order.ExclusionReason == ExcludedUnpaid),
            ExcludedRefundedCount = excluded.Count(order => order.ExclusionReason == ExcludedRefunded)
        };
    }

    private static FunnelSummary BuildFunnel(IReadOnlyCollection<SalesOrderRecord> included, IReadOnlyCollection<SalesOrderRecord> excluded) =>
        new()
        {
            ObservedOrderCount = included.Count + excluded.Count,
            IncludedOrderCount = included.Count,
            AwaitingCollectionStatusCount = included.Count(order => string.Equals(order.Status, "AWAITING_COLLECTION", StringComparison.OrdinalIgnoreCase)),
            ExcludedCancelledCount = excluded.Count(order => order.ExclusionReason == ExcludedCancelled),
            ExcludedUnpaidCount = excluded.Count(order => order.ExclusionReason == ExcludedUnpaid),
            ExcludedRefundedCount = excluded.Count(order => order.ExclusionReason == ExcludedRefunded)
        };

    private static BusinessCompassSummary BuildBusinessCompass(
        IReadOnlyCollection<SalesOrderRecord> included,
        OverviewSummary overview,
        (DateTimeOffset FromUtc, DateTimeOffset ToUtc) range)
    {
        var totalDays = Math.Max(1m, (decimal)Math.Ceiling((range.ToUtc - range.FromUtc).TotalDays + 1));
        var paidAmount = included.Sum(order => order.PaidAmount);
        var ordersPerDay = included.Count / totalDays;
        var revenuePerDay = paidAmount / totalDays;
        var paymentMethodCount = included
            .Select(order => order.PaymentMethod)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var axes = new List<BusinessCompassAxisSummary>
        {
            CreateAxis("revenue", "销售强度", ClampScore(revenuePerDay / 50000m * 100m), $"{revenuePerDay:N0} / 天", "每天能产生多少真实实付销售额"),
            CreateAxis("volume", "订单密度", ClampScore(ordersPerDay / 15m * 100m), $"{ordersPerDay:N1} 单 / 天", "每天计入销售的订单密度"),
            CreateAxis("aov", "客单价", ClampScore(overview.AverageOrderValue / 4000m * 100m), $"{overview.AverageOrderValue:N0}", "订单质量与客单价表现"),
            CreateAxis("loyalty", "复购黏性", ClampScore(overview.RepeatBuyerRate), $"{overview.RepeatBuyerRate:N1}%", "重复买家的贡献度"),
            CreateAxis("payment_mix", "支付多样性", ClampScore(paymentMethodCount / 5m * 100m), $"{paymentMethodCount} 种", "支付方式越丰富，支付完成率通常越稳"),
            CreateAxis("coverage", "数据完整度", ClampScore(overview.HandleCoverageRate), $"{overview.HandleCoverageRate:N1}%", "已经桥接出买家用户名的覆盖率")
        };

        return new BusinessCompassSummary
        {
            OverallScore = axes.Count == 0 ? 0m : Math.Round(axes.Average(axis => axis.Score), 1),
            Axes = axes
        };
    }

    private static SalesInsightsSummary BuildInsights(
        IReadOnlyCollection<SalesOrderRecord> included,
        AnalyticsSettings settings,
        string currency)
    {
        var daily = BuildDaily(included, settings);
        var hourly = BuildHourly(included);
        var stores = BuildStoreBreakdown(included, settings);
        var payments = BuildPaymentBreakdown(included, settings);
        var buyers = BuildTopBuyers(included, settings);
        var products = BuildTopProducts(included, settings);

        var bestDay = daily.OrderByDescending(item => item.PaidAmount).FirstOrDefault();
        var bestHour = hourly.OrderByDescending(item => item.PaidAmount).FirstOrDefault();
        var bestStore = stores.OrderByDescending(item => item.PaidAmount).FirstOrDefault();
        var bestPayment = payments.OrderByDescending(item => item.PaidAmount).FirstOrDefault();
        var bestBuyer = buyers.OrderByDescending(item => item.PaidAmount).FirstOrDefault();
        var bestProduct = products.OrderByDescending(item => item.SalesAmount).FirstOrDefault();

        return new SalesInsightsSummary
        {
            BestDay = new InsightMetricSummary { Title = "最强销售日", Label = bestDay?.Date ?? "-", Value = bestDay is null ? "-" : FormatMoney(bestDay.PaidAmount, currency), Note = bestDay is null ? "暂无数据" : $"{bestDay.OrderCount} 单" },
            BestHour = new InsightMetricSummary { Title = "最强支付时段", Label = bestHour?.HourLabel ?? "-", Value = bestHour is null ? "-" : FormatMoney(bestHour.PaidAmount, currency), Note = bestHour is null ? "暂无数据" : $"{bestHour.OrderCount} 单" },
            BestStore = new InsightMetricSummary { Title = "主力店铺", Label = bestStore?.StoreName ?? "-", Value = bestStore is null ? "-" : FormatMoney(bestStore.PaidAmount, currency), Note = bestStore is null ? "暂无数据" : $"{bestStore.OrderCount} 单 · 客单价 {FormatMoney(bestStore.AverageOrderValue, currency)}" },
            BestPayment = new InsightMetricSummary { Title = "主力支付方式", Label = bestPayment?.PaymentMethod ?? "-", Value = bestPayment is null ? "-" : FormatMoney(bestPayment.PaidAmount, currency), Note = bestPayment is null ? "暂无数据" : $"{bestPayment.OrderCount} 单 · 占比 {bestPayment.PaidAmountShareRate:N1}%" },
            BestBuyer = new InsightMetricSummary { Title = "最高价值买家", Label = bestBuyer?.BuyerLabel ?? "-", Value = bestBuyer is null ? "-" : FormatMoney(bestBuyer.PaidAmount, currency), Note = bestBuyer is null ? "暂无数据" : $"{bestBuyer.OrderCount} 单" },
            BestProduct = new InsightMetricSummary { Title = "热销商品", Label = bestProduct?.DisplayName ?? "-", Value = bestProduct is null ? "-" : FormatMoney(bestProduct.SalesAmount, currency), Note = bestProduct is null ? "暂无数据" : $"{bestProduct.OrderCount} 单涉及" }
        };
    }

    private static ReconciliationSummary BuildReconciliation(
        IReadOnlyCollection<SalesOrderRecord> included,
        AnalyticsSettings settings)
    {
        var basePaid = included.Sum(order => order.PaidAmount);
        var tiktokDiscount = settings.IncludeTikTokDiscount ? included.Sum(order => order.TikTokDiscountAmount) : 0m;
        var buyerShipping = settings.IncludeBuyerShippingFee ? included.Sum(order => order.BuyerShippingFeeAmount) : 0m;
        var platformFee = settings.DeductPlatformFee && settings.PlatformFeeRate > 0m ? Math.Round(basePaid * settings.PlatformFeeRate / 100m, 2) : 0m;
        var logisticsCost = settings.DeductLogisticsCost && settings.LogisticsCostPerOrder > 0m ? Math.Round(included.Count * settings.LogisticsCostPerOrder, 2) : 0m;

        var completedOrders = included.Where(IsSettlementCompleted).ToList();
        var pendingOrders = included.Where(order => !IsSettlementCompleted(order)).ToList();
        var calculatedShippingOrders = included.Where(HasCalculatedShippingFee).ToList();
        var calculatedShippingAmount = calculatedShippingOrders.Sum(order => order.BuyerShippingFeeAmount);
        var fallbackShippingOrderCount = Math.Max(0, included.Count - calculatedShippingOrders.Count);
        var actualShippingAmount = calculatedShippingAmount + (settings.LogisticsCostPerOrder > 0m ? fallbackShippingOrderCount * settings.LogisticsCostPerOrder : 0m);
        var settledActualShippingAmount = completedOrders.Sum(order => HasCalculatedShippingFee(order) ? order.BuyerShippingFeeAmount : Math.Max(0m, settings.LogisticsCostPerOrder));
        var estimatedReceivableAfterEstimatedShipping = basePaid + tiktokDiscount - logisticsCost - platformFee;
        var actualReceivableAfterActualShipping = basePaid + tiktokDiscount - actualShippingAmount - platformFee;
        var actualSettledReceivableAfterActualShipping = completedOrders.Sum(order => BuildReceivableWithCalculatedShipping(order, settings));
        var actualPendingReceivableAfterActualShipping = pendingOrders.Sum(order => BuildReceivableWithCalculatedShipping(order, settings));

        return new ReconciliationSummary
        {
            BasePaidAmount = basePaid,
            TikTokDiscountAmount = tiktokDiscount,
            BuyerShippingFeeAmount = buyerShipping,
            CalculatedShippingFeeAmount = calculatedShippingAmount,
            EstimatedShippingFeeAmount = logisticsCost,
            ActualShippingFeeAmount = actualShippingAmount,
            EstimatedPlatformFeeAmount = platformFee,
            EstimatedLogisticsCostAmount = logisticsCost,
            EstimatedReceivableAmount = basePaid + tiktokDiscount + buyerShipping - platformFee - logisticsCost,
            EstimatedSettledReceivableAmount = completedOrders.Sum(order => BuildEstimatedReceivable(order, settings)),
            EstimatedPendingReceivableAmount = pendingOrders.Sum(order => BuildEstimatedReceivable(order, settings)),
            EstimatedReceivableAfterEstimatedShippingAmount = estimatedReceivableAfterEstimatedShipping,
            ActualReceivableAfterActualShippingAmount = actualReceivableAfterActualShipping,
            ActualSettledReceivableAfterActualShippingAmount = actualSettledReceivableAfterActualShipping,
            ActualPendingReceivableAfterActualShippingAmount = actualPendingReceivableAfterActualShipping,
            SettledActualShippingFeeAmount = settledActualShippingAmount,
            SettledAverageShippingFeeAmount = completedOrders.Count == 0 ? 0m : Math.Round(settledActualShippingAmount / completedOrders.Count, 2),
            CalculatedShippingOrderCount = calculatedShippingOrders.Count,
            ReconcilableOrderCount = included.Count,
            CompletedOrderCount = completedOrders.Count,
            PendingSettlementOrderCount = pendingOrders.Count,
            EstimatedShippingOrderCount = included.Count,
            ActualShippingFallbackOrderCount = fallbackShippingOrderCount,
            SettlementCompletionRate = included.Count == 0 ? 0m : completedOrders.Count * 100m / included.Count,
            Note = $"口径说明：预估运费 = {included.Count} 单 × {settings.LogisticsCostPerOrder:0.##}；实际运费 = 平台已算好运费 + 剩余未出运费订单按单票物流成本补估；预估可回款 = 实际支付 + 折扣 - 预估运费 - 平台佣金；实际可回款 = 实际支付 + 折扣 - 实际运费 - 平台佣金。"
        };
    }

    private static List<MonthlySummary> BuildMonthly(IEnumerable<SalesOrderRecord> included, AnalyticsSettings settings) =>
        included.GroupBy(order => order.LocalPaidMonth)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key)
            .Select(group => new MonthlySummary
            {
                Month = group.Key,
                OrderCount = group.Count(),
                PaidAmount = group.Sum(item => item.PaidAmount),
                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                GrossWithDiscount = group.Sum(item => BuildDisplayedGross(item, settings))
            })
            .ToList();

    private static List<DailySummary> BuildDaily(IEnumerable<SalesOrderRecord> included, AnalyticsSettings settings) =>
        included.GroupBy(order => order.LocalPaidDate)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key)
            .Select(group => new DailySummary
            {
                Date = group.Key,
                OrderCount = group.Count(),
                PaidAmount = group.Sum(item => item.PaidAmount),
                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                GrossWithDiscount = group.Sum(item => BuildDisplayedGross(item, settings))
            })
            .ToList();

    private static List<HourlySummary> BuildHourly(IEnumerable<SalesOrderRecord> included) =>
        included.Where(order => order.LocalPaidHour is not null)
            .GroupBy(order => order.LocalPaidHour!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new HourlySummary
            {
                HourLabel = $"{group.Key:00}:00",
                OrderCount = group.Count(),
                PaidAmount = group.Sum(item => item.PaidAmount)
            })
            .ToList();

    private static List<StoreBreakdownSummary> BuildStoreBreakdown(IEnumerable<SalesOrderRecord> included, AnalyticsSettings settings) =>
        included.GroupBy(order => new { order.StoreKey, order.StoreName })
            .OrderByDescending(group => group.Sum(item => item.PaidAmount))
            .Select(group => new StoreBreakdownSummary
            {
                StoreKey = group.Key.StoreKey,
                StoreName = group.Key.StoreName,
                OrderCount = group.Count(),
                PaidAmount = group.Sum(item => item.PaidAmount),
                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                GrossWithDiscount = group.Sum(item => BuildDisplayedGross(item, settings)),
                AverageOrderValue = group.Any() ? group.Sum(item => item.PaidAmount) / group.Count() : 0m,
                UniqueBuyerCount = group.Select(ResolveBuyerKey).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .ToList();

    private static List<PaymentBreakdownSummary> BuildPaymentBreakdown(IReadOnlyCollection<SalesOrderRecord> included, AnalyticsSettings settings)
    {
        var totalPaidAmount = included.Sum(order => order.PaidAmount);
        return included.GroupBy(order => string.IsNullOrWhiteSpace(order.PaymentMethod) ? "Unknown" : order.PaymentMethod)
            .OrderByDescending(group => group.Sum(item => item.PaidAmount))
            .Select(group => new PaymentBreakdownSummary
            {
                PaymentMethod = group.Key,
                OrderCount = group.Count(),
                PaidAmount = group.Sum(item => item.PaidAmount),
                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                GrossWithDiscount = group.Sum(item => BuildDisplayedGross(item, settings)),
                PaidAmountShareRate = totalPaidAmount == 0m ? 0m : group.Sum(item => item.PaidAmount) * 100m / totalPaidAmount
            })
            .ToList();
    }

    private static List<StatusBreakdownSummary> BuildStatusBreakdown(IEnumerable<SalesOrderRecord> orders) =>
        orders.GroupBy(order => order.Status)
            .OrderByDescending(group => group.Count())
            .Select(group =>
            {
                var first = group.First();
                return new StatusBreakdownSummary
                {
                    Status = string.IsNullOrWhiteSpace(group.Key) ? "UNKNOWN" : group.Key,
                    Classification = first.IncludedInSales ? "included" : first.ExclusionReason,
                    OrderCount = group.Count(),
                    PaidAmount = group.Sum(item => item.PaidAmount)
                };
            })
            .ToList();

    private static List<BuyerBreakdownSummary> BuildTopBuyers(IEnumerable<SalesOrderRecord> included, AnalyticsSettings settings) =>
        included.Where(order => !string.IsNullOrWhiteSpace(order.BuyerHandle))
            .Select(order => new
            {
                BuyerHandle = order.BuyerHandle.Trim(),
                order.StoreName,
                order.PaidAmount,
                order.TikTokDiscountAmount,
                GrossAmount = BuildDisplayedGross(order, settings)
            })
            .GroupBy(item => item.BuyerHandle, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(item => item.PaidAmount))
            .Take(20)
            .Select(group => new BuyerBreakdownSummary
            {
                BuyerLabel = group.Key,
                OrderCount = group.Count(),
                PaidAmount = group.Sum(item => item.PaidAmount),
                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                GrossWithDiscount = group.Sum(item => item.GrossAmount),
                StoreCount = group.Select(item => item.StoreName).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .ToList();

    private static List<ProductBreakdownSummary> BuildTopProducts(IEnumerable<SalesOrderRecord> included, AnalyticsSettings settings) =>
        included.SelectMany(order => order.Items.Select(item => new
            {
                item.ProductId,
                item.SkuId,
                item.ProductName,
                item.SkuName,
                item.DisplayName,
                item.Quantity,
                item.SalePrice,
                item.TikTokDiscountAmount,
                order.OrderId,
                BuyerShippingShare = order.ItemLineCount == 0 ? 0m : order.BuyerShippingFeeAmount / order.ItemLineCount
            }))
            .GroupBy(item => new { item.ProductId, item.SkuId, item.ProductName, item.SkuName, item.DisplayName })
            .OrderByDescending(group => group.Sum(item => item.SalePrice))
            .Take(20)
            .Select(group => new ProductBreakdownSummary
            {
                ProductId = group.Key.ProductId,
                ProductName = group.Key.ProductName,
                SkuName = group.Key.SkuName,
                DisplayName = group.Key.DisplayName,
                OrderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ItemLineCount = group.Count(),
                Quantity = group.Sum(item => item.Quantity),
                SalesAmount = group.Sum(item => item.SalePrice),
                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                GrossWithDiscount = group.Sum(item => item.SalePrice + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m) + (settings.IncludeBuyerShippingFee ? item.BuyerShippingShare : 0m))
            })
            .ToList();

    private static List<ProductIdBreakdownSummary> BuildProductIdBreakdown(
        IEnumerable<SalesOrderRecord> included,
        AnalyticsSettings settings) =>
        included.SelectMany(order => order.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.ProductId))
                .Select(item => new
                {
                    order.StoreKey,
                    order.StoreName,
                    order.OrderId,
                    item.ProductId,
                    item.ProductName,
                    item.SkuId,
                    item.SkuName,
                    item.DisplayName,
                    item.Quantity,
                    item.SalePrice,
                    item.TikTokDiscountAmount,
                    BuyerShippingShare = order.ItemLineCount == 0 ? 0m : order.BuyerShippingFeeAmount / order.ItemLineCount
                }))
            .GroupBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(item => item.SalePrice))
            .ThenBy(group => ResolveProductName(group), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var productName = ResolveProductName(group);
                return new ProductIdBreakdownSummary
                {
                    ProductId = group.Key,
                    ProductName = productName,
                    DisplayName = string.IsNullOrWhiteSpace(productName) ? group.Key : $"{productName} ({group.Key})",
                    OrderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    ItemLineCount = group.Count(),
                    Quantity = group.Sum(item => item.Quantity),
                    SkuCount = group.Select(item => item.SkuId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    StoreCount = group.Select(item => item.StoreKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    SalesAmount = group.Sum(item => item.SalePrice),
                    TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                    GrossWithDiscount = group.Sum(item => item.SalePrice + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m) + (settings.IncludeBuyerShippingFee ? item.BuyerShippingShare : 0m)),
                    StoreBreakdown = group
                        .GroupBy(item => new { item.StoreKey, item.StoreName })
                        .OrderByDescending(storeGroup => storeGroup.Sum(item => item.SalePrice))
                        .Select(storeGroup => new ProductIdStoreBreakdownSummary
                        {
                            StoreKey = storeGroup.Key.StoreKey,
                            StoreName = storeGroup.Key.StoreName,
                            OrderCount = storeGroup.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                            ItemLineCount = storeGroup.Count(),
                            Quantity = storeGroup.Sum(item => item.Quantity),
                            SalesAmount = storeGroup.Sum(item => item.SalePrice),
                            TikTokDiscountAmount = storeGroup.Sum(item => item.TikTokDiscountAmount),
                            GrossWithDiscount = storeGroup.Sum(item => item.SalePrice + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m) + (settings.IncludeBuyerShippingFee ? item.BuyerShippingShare : 0m))
                        })
                        .ToList(),
                    SkuBreakdown = group
                        .GroupBy(item => new { item.ProductId, item.SkuId, item.SkuName, item.DisplayName })
                        .OrderByDescending(skuGroup => skuGroup.Sum(item => item.SalePrice))
                        .ThenBy(skuGroup => skuGroup.Key.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .Select(skuGroup => new ProductSkuBreakdownSummary
                        {
                            ProductId = skuGroup.Key.ProductId,
                            SkuId = skuGroup.Key.SkuId,
                            SkuName = skuGroup.Key.SkuName,
                            DisplayName = skuGroup.Key.DisplayName,
                            OrderCount = skuGroup.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                            ItemLineCount = skuGroup.Count(),
                            Quantity = skuGroup.Sum(item => item.Quantity),
                            SalesAmount = skuGroup.Sum(item => item.SalePrice),
                            TikTokDiscountAmount = skuGroup.Sum(item => item.TikTokDiscountAmount),
                            GrossWithDiscount = skuGroup.Sum(item => item.SalePrice + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m) + (settings.IncludeBuyerShippingFee ? item.BuyerShippingShare : 0m))
                        })
                        .ToList()
                };
            })
            .ToList();

    private static List<ProductPerformanceSummary> BuildTrackedProductPerformance(
        IEnumerable<SalesOrderRecord> included,
        AnalyticsSettings settings,
        IReadOnlyCollection<TrackedProductDefinition> trackedProducts)
    {
        var definitionLookup = trackedProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductId))
            .GroupBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        if (definitionLookup.Count == 0)
        {
            return [];
        }

        var rows = BuildProductPerformanceLines(included, settings, definitionLookup);

        return trackedProducts
            .Select(definition =>
            {
                var productRows = rows
                    .Where(item => string.Equals(item.ProductId, definition.ProductId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var productName = productRows
                    .Select(item => item.ProductName?.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Key)
                    .FirstOrDefault() ?? string.Empty;

                var settledRows = productRows.Where(item => item.SettlementCompleted).ToList();
                var pendingRows = productRows.Where(item => !item.SettlementCompleted).ToList();

                return new ProductPerformanceSummary
                {
                    ProductId = definition.ProductId,
                    Label = string.IsNullOrWhiteSpace(definition.Label) ? definition.ProductId : definition.Label,
                    ProductName = productName,
                    DisplayName = string.IsNullOrWhiteSpace(productName) ? definition.ProductId : $"{productName} ({definition.ProductId})",
                    OrderCount = productRows.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    ItemLineCount = productRows.Count,
                    Quantity = productRows.Sum(item => item.Quantity),
                    SkuCount = productRows.Select(item => item.SkuId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    StoreCount = productRows.Select(item => item.StoreKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    PaidAmount = productRows.Sum(item => item.PaidAmount),
                    TikTokDiscountAmount = productRows.Sum(item => item.TikTokDiscountAmount),
                    BuyerShippingFeeAmount = productRows.Sum(item => item.BuyerShippingFeeAmount),
                    EstimatedPlatformFeeAmount = productRows.Sum(item => item.EstimatedPlatformFeeAmount),
                    EstimatedLogisticsCostAmount = productRows.Sum(item => item.EstimatedLogisticsCostAmount),
                    EstimatedReceivableAmount = productRows.Sum(item => item.EstimatedReceivableAmount),
                    EstimatedSettledReceivableAmount = settledRows.Sum(item => item.EstimatedReceivableAmount),
                    EstimatedPendingReceivableAmount = pendingRows.Sum(item => item.EstimatedReceivableAmount),
                    GrossWithDiscount = productRows.Sum(item => item.GrossWithDiscount),
                    CompletedOrderCount = settledRows.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    PendingSettlementOrderCount = pendingRows.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    SettlementCompletionRate = productRows.Count == 0
                        ? 0m
                        : settledRows.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count() * 100m
                            / productRows.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Monthly = productRows
                        .Where(item => !string.IsNullOrWhiteSpace(item.LocalPaidMonth))
                        .GroupBy(item => item.LocalPaidMonth, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(group => group.Key)
                        .Select(group => new ProductPerformanceMonthlySummary
                        {
                            Month = group.Key,
                            OrderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                            Quantity = group.Sum(item => item.Quantity),
                            PaidAmount = group.Sum(item => item.PaidAmount),
                            TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                            EstimatedReceivableAmount = group.Sum(item => item.EstimatedReceivableAmount),
                            GrossWithDiscount = group.Sum(item => item.GrossWithDiscount)
                        })
                        .ToList(),
                    Daily = productRows
                        .Where(item => !string.IsNullOrWhiteSpace(item.LocalPaidDate))
                        .GroupBy(item => item.LocalPaidDate, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(group => group.Key)
                        .Select(group => new ProductPerformanceDailySummary
                        {
                            Date = group.Key,
                            OrderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                            Quantity = group.Sum(item => item.Quantity),
                            PaidAmount = group.Sum(item => item.PaidAmount),
                            TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                            EstimatedReceivableAmount = group.Sum(item => item.EstimatedReceivableAmount),
                            GrossWithDiscount = group.Sum(item => item.GrossWithDiscount)
                        })
                        .ToList(),
                    StoreBreakdown = productRows
                        .GroupBy(item => new { item.StoreKey, item.StoreName })
                        .OrderByDescending(group => group.Sum(item => item.PaidAmount))
                        .Select(group =>
                        {
                            var groupSettled = group.Where(item => item.SettlementCompleted).ToList();
                            var groupPending = group.Where(item => !item.SettlementCompleted).ToList();
                            return new ProductPerformanceStoreSummary
                            {
                                StoreKey = group.Key.StoreKey,
                                StoreName = group.Key.StoreName,
                                OrderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                                ItemLineCount = group.Count(),
                                Quantity = group.Sum(item => item.Quantity),
                                PaidAmount = group.Sum(item => item.PaidAmount),
                                TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                                BuyerShippingFeeAmount = group.Sum(item => item.BuyerShippingFeeAmount),
                                EstimatedPlatformFeeAmount = group.Sum(item => item.EstimatedPlatformFeeAmount),
                                EstimatedLogisticsCostAmount = group.Sum(item => item.EstimatedLogisticsCostAmount),
                                EstimatedReceivableAmount = group.Sum(item => item.EstimatedReceivableAmount),
                                EstimatedSettledReceivableAmount = groupSettled.Sum(item => item.EstimatedReceivableAmount),
                                EstimatedPendingReceivableAmount = groupPending.Sum(item => item.EstimatedReceivableAmount),
                                GrossWithDiscount = group.Sum(item => item.GrossWithDiscount),
                                CompletedOrderCount = groupSettled.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                                PendingSettlementOrderCount = groupPending.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                                SettlementCompletionRate = group.Any()
                                    ? groupSettled.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count() * 100m
                                        / group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                                    : 0m
                            };
                        })
                        .ToList()
                };
            })
            .OrderByDescending(item => item.PaidAmount)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ProductPerformanceLine> BuildProductPerformanceLines(
        IEnumerable<SalesOrderRecord> included,
        AnalyticsSettings settings,
        IReadOnlyDictionary<string, TrackedProductDefinition> definitionLookup) =>
        definitionLookup.Count == 0
            ? []
            : included
                .SelectMany(order => order.Items
                    .Where(item => !string.IsNullOrWhiteSpace(item.ProductId) && definitionLookup.ContainsKey(item.ProductId))
                    .Select(item =>
                    {
                        var buyerShippingShare = order.ItemLineCount == 0 ? 0m : order.BuyerShippingFeeAmount / order.ItemLineCount;
                        var buyerShippingAmount = settings.IncludeBuyerShippingFee ? buyerShippingShare : 0m;
                        var calculatedShippingAmount = settings.IncludeBuyerShippingFee && HasCalculatedShippingFee(order) ? buyerShippingShare : 0m;
                        var platformFeeAmount = settings.DeductPlatformFee && settings.PlatformFeeRate > 0m
                            ? Math.Round(item.SalePrice * settings.PlatformFeeRate / 100m, 2)
                            : 0m;
                        var logisticsCostAmount = settings.DeductLogisticsCost && settings.LogisticsCostPerOrder > 0m && order.ItemLineCount > 0
                            ? Math.Round(settings.LogisticsCostPerOrder / order.ItemLineCount, 2)
                            : 0m;
                        var receivableAmount = item.SalePrice
                            + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m)
                            + buyerShippingAmount
                            - platformFeeAmount
                            - logisticsCostAmount;
                        var calculatedShippingReceivableAmount = item.SalePrice
                            + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m)
                            + calculatedShippingAmount
                            - platformFeeAmount;

                        return new ProductPerformanceLine
                        {
                            ProductId = item.ProductId,
                            Label = definitionLookup[item.ProductId].Label,
                            ProductName = item.ProductName,
                            StoreKey = order.StoreKey,
                            StoreName = order.StoreName,
                            OrderId = order.OrderId,
                            Quantity = item.Quantity,
                            SkuId = item.SkuId,
                            SkuName = item.SkuName,
                            PaidAmount = item.SalePrice,
                            TikTokDiscountAmount = item.TikTokDiscountAmount,
                            LocalPaidDate = order.LocalPaidDate,
                            LocalPaidMonth = order.LocalPaidMonth,
                            BuyerShippingFeeAmount = buyerShippingAmount,
                            CalculatedShippingFeeAmount = calculatedShippingAmount,
                            EstimatedPlatformFeeAmount = platformFeeAmount,
                            EstimatedLogisticsCostAmount = logisticsCostAmount,
                            EstimatedReceivableAmount = receivableAmount,
                            EstimatedReceivableWithCalculatedShippingAmount = calculatedShippingReceivableAmount,
                            GrossWithDiscount = item.SalePrice
                                + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m)
                                + buyerShippingAmount,
                            SettlementCompleted = IsSettlementCompleted(order)
                        };
                    }))
                .ToList();

    private static ProductPerformanceTotalsSummary BuildTrackedProductTotals(IReadOnlyCollection<ProductPerformanceSummary> products)
    {
        var orderCount = products.Sum(item => item.OrderCount);
        var completedOrderCount = products.Sum(item => item.CompletedOrderCount);
        var pendingOrderCount = products.Sum(item => item.PendingSettlementOrderCount);

        return new ProductPerformanceTotalsSummary
        {
            ProductCount = products.Count,
            OrderCount = orderCount,
            Quantity = products.Sum(item => item.Quantity),
            PaidAmount = products.Sum(item => item.PaidAmount),
            TikTokDiscountAmount = products.Sum(item => item.TikTokDiscountAmount),
            BuyerShippingFeeAmount = products.Sum(item => item.BuyerShippingFeeAmount),
            EstimatedPlatformFeeAmount = products.Sum(item => item.EstimatedPlatformFeeAmount),
            EstimatedLogisticsCostAmount = products.Sum(item => item.EstimatedLogisticsCostAmount),
            EstimatedReceivableAmount = products.Sum(item => item.EstimatedReceivableAmount),
            EstimatedSettledReceivableAmount = products.Sum(item => item.EstimatedSettledReceivableAmount),
            EstimatedPendingReceivableAmount = products.Sum(item => item.EstimatedPendingReceivableAmount),
            CompletedOrderCount = completedOrderCount,
            PendingSettlementOrderCount = pendingOrderCount,
            SettlementCompletionRate = orderCount == 0 ? 0m : completedOrderCount * 100m / orderCount
        };
    }

    private static StreamerCompensationSummary BuildStreamerCompensationSummary(
        StreamerRuleDefinition rule,
        IReadOnlyCollection<ProductPerformanceLine> lines,
        IReadOnlyList<string> months,
        decimal cnyToJpyRate,
        string currency)
    {
        var orderCount = lines.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var completedOrderCount = lines.Where(item => item.SettlementCompleted).Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var pendingOrderCount = lines.Where(item => !item.SettlementCompleted).Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var baseSalaryPerMonthJpy = ConvertCurrencyToJpy(rule.BaseSalaryAmount, rule.BaseSalaryCurrency, cnyToJpyRate);
        var monthly = months
            .Select(month =>
            {
                var monthRows = lines.Where(item => string.Equals(item.LocalPaidMonth, month, StringComparison.OrdinalIgnoreCase)).ToList();
                var commissionAmountJpy = Math.Round(monthRows.Sum(item => item.PaidAmount) * rule.CommissionRate, 2);
                var commissionAmountRmb = ConvertJpyToRmb(commissionAmountJpy, cnyToJpyRate);
                var salaryTotalAmountJpy = baseSalaryPerMonthJpy + commissionAmountJpy;
                var salaryTotalAmountRmb = Math.Round(rule.BaseSalaryAmount + commissionAmountRmb, 2);
                var estimatedLogisticsReceivable = monthRows.Sum(item => item.EstimatedReceivableAmount);
                var calculatedShippingReceivable = monthRows.Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount);

                return new StreamerCompensationMonthlySummary
                {
                    Month = month,
                    OrderCount = monthRows.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Quantity = monthRows.Sum(item => item.Quantity),
                    PaidAmount = monthRows.Sum(item => item.PaidAmount),
                    TikTokDiscountAmount = monthRows.Sum(item => item.TikTokDiscountAmount),
                    CalculatedShippingFeeAmount = monthRows.Sum(item => item.CalculatedShippingFeeAmount),
                    EstimatedReceivableAmount = estimatedLogisticsReceivable,
                    EstimatedReceivableWithEstimatedLogisticsAmount = estimatedLogisticsReceivable,
                    EstimatedReceivableWithCalculatedShippingAmount = calculatedShippingReceivable,
                    BaseSalaryAmount = rule.BaseSalaryAmount,
                    BaseSalaryCurrency = NormalizeSalaryCurrency(rule.BaseSalaryCurrency),
                    CommissionAmountRmb = commissionAmountRmb,
                    SalaryTotalAmountRmb = salaryTotalAmountRmb,
                    BaseSalaryAmountJpy = baseSalaryPerMonthJpy,
                    CommissionAmountJpy = commissionAmountJpy,
                    SalaryTotalAmountJpy = salaryTotalAmountJpy,
                    ProfitBeforeHiddenCostJpy = estimatedLogisticsReceivable - salaryTotalAmountJpy,
                    ProfitBeforeHiddenCostWithEstimatedLogisticsJpy = estimatedLogisticsReceivable - salaryTotalAmountJpy,
                    ProfitBeforeHiddenCostWithCalculatedShippingJpy = calculatedShippingReceivable - salaryTotalAmountJpy
                };
            })
            .ToList();

        var storeBreakdown = BuildStoreBreakdownFromLines(lines);
        var estimatedReceivableAmount = lines.Sum(item => item.EstimatedReceivableAmount);
        var calculatedShippingReceivableAmount = lines.Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount);
        var salaryBaseAmountRmb = monthly.Sum(item => item.BaseSalaryAmount);
        var commissionAmountTotalRmb = monthly.Sum(item => item.CommissionAmountRmb);
        var salaryTotalAmountRmbTotal = monthly.Sum(item => item.SalaryTotalAmountRmb);
        var salaryBaseAmountJpy = monthly.Sum(item => item.BaseSalaryAmountJpy);
        var commissionAmountTotalJpy = monthly.Sum(item => item.CommissionAmountJpy);
        var salaryTotalAmountJpyTotal = monthly.Sum(item => item.SalaryTotalAmountJpy);

        return new StreamerCompensationSummary
        {
            Key = rule.Key,
            Label = rule.Label,
            Note = rule.Note,
            IsSelfOwned = false,
            BaseSalaryAmount = Math.Round(rule.BaseSalaryAmount * months.Count, 2),
            BaseSalaryCurrency = NormalizeSalaryCurrency(rule.BaseSalaryCurrency),
            CommissionAmountRmb = commissionAmountTotalRmb,
            SalaryTotalAmountRmb = salaryTotalAmountRmbTotal,
            BaseSalaryAmountJpy = salaryBaseAmountJpy,
            CommissionRate = rule.CommissionRate,
            CommissionLabel = rule.CommissionLabel,
            CommissionAmountJpy = commissionAmountTotalJpy,
            SalaryTotalAmountJpy = salaryTotalAmountJpyTotal,
            ProductCount = rule.ProductIds.Count,
            OrderCount = orderCount,
            Quantity = lines.Sum(item => item.Quantity),
            PaidAmount = lines.Sum(item => item.PaidAmount),
            TikTokDiscountAmount = lines.Sum(item => item.TikTokDiscountAmount),
            BuyerShippingFeeAmount = lines.Sum(item => item.BuyerShippingFeeAmount),
            CalculatedShippingFeeAmount = lines.Sum(item => item.CalculatedShippingFeeAmount),
            EstimatedPlatformFeeAmount = lines.Sum(item => item.EstimatedPlatformFeeAmount),
            EstimatedLogisticsCostAmount = lines.Sum(item => item.EstimatedLogisticsCostAmount),
            EstimatedReceivableAmount = estimatedReceivableAmount,
            EstimatedSettledReceivableAmount = lines.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedPendingReceivableAmount = lines.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedReceivableWithEstimatedLogisticsAmount = estimatedReceivableAmount,
            EstimatedSettledReceivableWithEstimatedLogisticsAmount = lines.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedPendingReceivableWithEstimatedLogisticsAmount = lines.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedReceivableWithCalculatedShippingAmount = calculatedShippingReceivableAmount,
            EstimatedSettledReceivableWithCalculatedShippingAmount = lines.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
            EstimatedPendingReceivableWithCalculatedShippingAmount = lines.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
            ProfitBeforeHiddenCostJpy = estimatedReceivableAmount - salaryTotalAmountJpyTotal,
            ProfitAfterHiddenCostJpy = estimatedReceivableAmount - salaryTotalAmountJpyTotal,
            ProfitBeforeHiddenCostWithEstimatedLogisticsJpy = estimatedReceivableAmount - salaryTotalAmountJpyTotal,
            ProfitAfterHiddenCostWithEstimatedLogisticsJpy = estimatedReceivableAmount - salaryTotalAmountJpyTotal,
            ProfitBeforeHiddenCostWithCalculatedShippingJpy = calculatedShippingReceivableAmount - salaryTotalAmountJpyTotal,
            ProfitAfterHiddenCostWithCalculatedShippingJpy = calculatedShippingReceivableAmount - salaryTotalAmountJpyTotal,
            SettlementCompletionRate = orderCount == 0 ? 0m : completedOrderCount * 100m / orderCount,
            CompletedOrderCount = completedOrderCount,
            PendingSettlementOrderCount = pendingOrderCount,
            ProductIds = rule.ProductIds
                .Select(productId => new TrackedProductDefinition
                {
                    ProductId = productId,
                    Label = productId
                })
                .ToList(),
            Monthly = monthly,
            StoreBreakdown = storeBreakdown
        };
    }

    private static StreamerCompensationSummary BuildSelfOwnedCompensationSummary(
        IReadOnlyCollection<ProductPerformanceLine> lines,
        IReadOnlyCollection<TrackedProductDefinition> selfOwnedProducts,
        IReadOnlyList<string> months,
        string currency)
    {
        var summary = BuildStreamerCompensationSummary(
            new StreamerRuleDefinition
            {
                Key = "self-owned",
                Label = "自营",
                Note = "其余链接按店铺自营统计，不计主播薪资。",
                BaseSalaryAmount = 0m,
                BaseSalaryCurrency = currency,
                CommissionRate = 0m,
                CommissionLabel = "0%",
                ProductIds = selfOwnedProducts.Select(item => item.ProductId).ToList()
            },
            lines,
            months,
            1m,
            currency);

        summary.IsSelfOwned = true;
        summary.ProductCount = selfOwnedProducts.Count;
        summary.ProductIds = selfOwnedProducts.ToList();
        return summary;
    }

    private static List<ProductPerformanceStoreSummary> BuildStoreBreakdownFromLines(IReadOnlyCollection<ProductPerformanceLine> lines) =>
        lines
            .GroupBy(item => new { item.StoreKey, item.StoreName })
            .OrderByDescending(group => group.Sum(item => item.PaidAmount))
            .Select(group =>
            {
                var completedOrderCount = group.Where(item => item.SettlementCompleted).Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var pendingOrderCount = group.Where(item => !item.SettlementCompleted).Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var orderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();

                return new ProductPerformanceStoreSummary
                {
                    StoreKey = group.Key.StoreKey,
                    StoreName = group.Key.StoreName,
                    OrderCount = orderCount,
                    ItemLineCount = group.Count(),
                    Quantity = group.Sum(item => item.Quantity),
                    PaidAmount = group.Sum(item => item.PaidAmount),
                    TikTokDiscountAmount = group.Sum(item => item.TikTokDiscountAmount),
                    BuyerShippingFeeAmount = group.Sum(item => item.BuyerShippingFeeAmount),
                    CalculatedShippingFeeAmount = group.Sum(item => item.CalculatedShippingFeeAmount),
                    EstimatedPlatformFeeAmount = group.Sum(item => item.EstimatedPlatformFeeAmount),
                    EstimatedLogisticsCostAmount = group.Sum(item => item.EstimatedLogisticsCostAmount),
                    EstimatedReceivableAmount = group.Sum(item => item.EstimatedReceivableAmount),
                    EstimatedSettledReceivableAmount = group.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
                    EstimatedPendingReceivableAmount = group.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
                    EstimatedReceivableWithCalculatedShippingAmount = group.Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
                    EstimatedSettledReceivableWithCalculatedShippingAmount = group.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
                    EstimatedPendingReceivableWithCalculatedShippingAmount = group.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
                    GrossWithDiscount = group.Sum(item => item.GrossWithDiscount),
                    SettlementCompletionRate = orderCount == 0 ? 0m : completedOrderCount * 100m / orderCount,
                    CompletedOrderCount = completedOrderCount,
                    PendingSettlementOrderCount = pendingOrderCount
                };
            })
            .ToList();

    private static List<string> BuildMonthSequence(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            return [];
        }

        var months = new List<string>();
        var cursor = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var end = new DateOnly(toDate.Year, toDate.Month, 1);
        while (cursor <= end)
        {
            months.Add($"{cursor:yyyy-MM}");
            cursor = cursor.AddMonths(1);
        }

        return months;
    }

    private static string NormalizeSalaryCurrency(string? currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "CNY" or "CNH" or "RMB" => "RMB",
            _ => "RMB"
        };
    }

    private static decimal ConvertCurrencyToJpy(decimal amount, string? currency, decimal cnyToJpyRate)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        var normalized = currency?.Trim().ToUpperInvariant() ?? "JPY";
        return normalized switch
        {
            "CNY" or "RMB" or "CNH" => Math.Round(amount * cnyToJpyRate, 2),
            _ => Math.Round(amount, 2)
        };
    }

    private static decimal ConvertJpyToRmb(decimal amountJpy, decimal cnyToJpyRate)
    {
        if (amountJpy <= 0m || cnyToJpyRate <= 0m)
        {
            return 0m;
        }

        return Math.Round(amountJpy / cnyToJpyRate, 2);
    }

    private static List<StreamerMonthlyProfitSummary> BuildStreamerMonthlyProfit(
        IReadOnlyCollection<StreamerCompensationSummary> streamers,
        StreamerCompensationSummary selfOwnedSummary,
        IReadOnlyList<string> months,
        decimal hiddenProcurementCostJpy,
        decimal totalPaidForAllocation)
    {
        var participants = streamers
            .Concat([selfOwnedSummary])
            .ToList();

        return months
            .Select(month =>
            {
                var monthRows = participants
                    .Select(summary => summary.Monthly.FirstOrDefault(item => string.Equals(item.Month, month, StringComparison.OrdinalIgnoreCase)))
                    .Where(item => item is not null)
                    .Cast<StreamerCompensationMonthlySummary>()
                    .ToList();
                var paidAmount = monthRows.Sum(item => item.PaidAmount);
                var hiddenCost = totalPaidForAllocation <= 0m
                    ? 0m
                    : Math.Round(hiddenProcurementCostJpy * (paidAmount / totalPaidForAllocation), 2);
                var estimatedLogisticsReceivable = monthRows.Sum(item => item.EstimatedReceivableWithEstimatedLogisticsAmount);
                var calculatedShippingReceivable = monthRows.Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount);
                var salaryBaseAmountRmb = monthRows.Sum(item => item.BaseSalaryAmount);
                var salaryCommissionAmountRmb = monthRows.Sum(item => item.CommissionAmountRmb);
                var salaryTotalAmountRmb = monthRows.Sum(item => item.SalaryTotalAmountRmb);
                var salaryBaseAmountJpy = monthRows.Sum(item => item.BaseSalaryAmountJpy);
                var salaryCommissionAmountJpy = monthRows.Sum(item => item.CommissionAmountJpy);
                var salaryTotalAmountJpy = monthRows.Sum(item => item.SalaryTotalAmountJpy);

                return new StreamerMonthlyProfitSummary
                {
                    Month = month,
                    PaidAmount = paidAmount,
                    TikTokDiscountAmount = monthRows.Sum(item => item.TikTokDiscountAmount),
                    CalculatedShippingFeeAmount = monthRows.Sum(item => item.CalculatedShippingFeeAmount),
                    EstimatedReceivableAmount = estimatedLogisticsReceivable,
                    EstimatedReceivableWithEstimatedLogisticsAmount = estimatedLogisticsReceivable,
                    EstimatedReceivableWithCalculatedShippingAmount = calculatedShippingReceivable,
                    SalaryBaseAmountRmb = salaryBaseAmountRmb,
                    SalaryCommissionAmountRmb = salaryCommissionAmountRmb,
                    SalaryTotalAmountRmb = salaryTotalAmountRmb,
                    SalaryBaseAmountJpy = salaryBaseAmountJpy,
                    SalaryCommissionAmountJpy = salaryCommissionAmountJpy,
                    SalaryTotalAmountJpy = salaryTotalAmountJpy,
                    HiddenProcurementCostJpy = hiddenCost,
                    ProfitBeforeHiddenCostJpy = estimatedLogisticsReceivable - salaryTotalAmountJpy,
                    ProfitAfterHiddenCostJpy = estimatedLogisticsReceivable - salaryTotalAmountJpy - hiddenCost,
                    ProfitBeforeHiddenCostWithEstimatedLogisticsJpy = estimatedLogisticsReceivable - salaryTotalAmountJpy,
                    ProfitAfterHiddenCostWithEstimatedLogisticsJpy = estimatedLogisticsReceivable - salaryTotalAmountJpy - hiddenCost,
                    ProfitBeforeHiddenCostWithCalculatedShippingJpy = calculatedShippingReceivable - salaryTotalAmountJpy,
                    ProfitAfterHiddenCostWithCalculatedShippingJpy = calculatedShippingReceivable - salaryTotalAmountJpy - hiddenCost
                };
            })
            .ToList();
    }

    private static StreamerCompensationTotalsSummary BuildStreamerCompensationTotals(
        IReadOnlyCollection<StreamerCompensationSummary> streamers,
        StreamerCompensationSummary selfOwnedSummary,
        IReadOnlyCollection<StreamerMonthlyProfitSummary> monthlyProfit,
        decimal hiddenProcurementCostJpy,
        IReadOnlyCollection<ProductPerformanceLine> productLines)
    {
        var orderCount = productLines.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var completedOrderCount = productLines.Where(item => item.SettlementCompleted).Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var pendingOrderCount = productLines.Where(item => !item.SettlementCompleted).Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var participants = streamers
            .Concat([selfOwnedSummary])
            .ToList();
        var salaryBaseAmountRmb = participants.Sum(item => item.BaseSalaryAmount);
        var salaryCommissionAmountRmb = participants.Sum(item => item.CommissionAmountRmb);
        var salaryTotalAmountRmb = participants.Sum(item => item.SalaryTotalAmountRmb);
        var salaryBaseAmountJpy = participants.Sum(item => item.BaseSalaryAmountJpy);
        var salaryCommissionAmountJpy = participants.Sum(item => item.CommissionAmountJpy);
        var salaryTotalAmountJpy = participants.Sum(item => item.SalaryTotalAmountJpy);
        var estimatedReceivableAmount = productLines.Sum(item => item.EstimatedReceivableAmount);
        var calculatedShippingReceivableAmount = productLines.Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount);

        return new StreamerCompensationTotalsSummary
        {
            ProductCount = productLines.Select(item => item.ProductId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            OrderCount = orderCount,
            Quantity = productLines.Sum(item => item.Quantity),
            PaidAmount = productLines.Sum(item => item.PaidAmount),
            TikTokDiscountAmount = productLines.Sum(item => item.TikTokDiscountAmount),
            BuyerShippingFeeAmount = productLines.Sum(item => item.BuyerShippingFeeAmount),
            CalculatedShippingFeeAmount = productLines.Sum(item => item.CalculatedShippingFeeAmount),
            EstimatedPlatformFeeAmount = productLines.Sum(item => item.EstimatedPlatformFeeAmount),
            EstimatedLogisticsCostAmount = productLines.Sum(item => item.EstimatedLogisticsCostAmount),
            EstimatedReceivableAmount = estimatedReceivableAmount,
            EstimatedSettledReceivableAmount = productLines.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedPendingReceivableAmount = productLines.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedReceivableWithEstimatedLogisticsAmount = estimatedReceivableAmount,
            EstimatedSettledReceivableWithEstimatedLogisticsAmount = productLines.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedPendingReceivableWithEstimatedLogisticsAmount = productLines.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableAmount),
            EstimatedReceivableWithCalculatedShippingAmount = calculatedShippingReceivableAmount,
            EstimatedSettledReceivableWithCalculatedShippingAmount = productLines.Where(item => item.SettlementCompleted).Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
            EstimatedPendingReceivableWithCalculatedShippingAmount = productLines.Where(item => !item.SettlementCompleted).Sum(item => item.EstimatedReceivableWithCalculatedShippingAmount),
            SalaryBaseAmountRmb = salaryBaseAmountRmb,
            SalaryCommissionAmountRmb = salaryCommissionAmountRmb,
            SalaryTotalAmountRmb = salaryTotalAmountRmb,
            SalaryBaseAmountJpy = salaryBaseAmountJpy,
            SalaryCommissionAmountJpy = salaryCommissionAmountJpy,
            SalaryTotalAmountJpy = salaryTotalAmountJpy,
            HiddenProcurementCostJpy = hiddenProcurementCostJpy,
            ProfitBeforeHiddenCostJpy = estimatedReceivableAmount - salaryTotalAmountJpy,
            ProfitAfterHiddenCostJpy = monthlyProfit.Sum(item => item.ProfitAfterHiddenCostJpy),
            ProfitBeforeHiddenCostWithEstimatedLogisticsJpy = estimatedReceivableAmount - salaryTotalAmountJpy,
            ProfitAfterHiddenCostWithEstimatedLogisticsJpy = monthlyProfit.Sum(item => item.ProfitAfterHiddenCostWithEstimatedLogisticsJpy),
            ProfitBeforeHiddenCostWithCalculatedShippingJpy = calculatedShippingReceivableAmount - salaryTotalAmountJpy,
            ProfitAfterHiddenCostWithCalculatedShippingJpy = monthlyProfit.Sum(item => item.ProfitAfterHiddenCostWithCalculatedShippingJpy),
            SettlementCompletionRate = orderCount == 0 ? 0m : completedOrderCount * 100m / orderCount,
            CompletedOrderCount = completedOrderCount,
            PendingSettlementOrderCount = pendingOrderCount
        };
    }

    private static string ResolveProductName<T>(IGrouping<string, T> group) where T : notnull
    {
        var productNameProperty = typeof(T).GetProperty("ProductName");
        if (productNameProperty is null)
        {
            return group.Key;
        }

        return group
            .Select(item => productNameProperty.GetValue(item)?.ToString()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(nameGroup => nameGroup.Count())
            .ThenBy(nameGroup => nameGroup.Key, StringComparer.OrdinalIgnoreCase)
            .Select(nameGroup => nameGroup.Key)
            .FirstOrDefault() ?? group.Key;
    }

    private static List<string> NormalizeProductIds(IReadOnlyCollection<string>? productIds) =>
        (productIds ?? [])
            .SelectMany(value => (value ?? string.Empty).Split(new[] { ',', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<ProductIdBreakdownSummary> ApplySelectedProductOrder(
        IReadOnlyList<ProductIdBreakdownSummary> products,
        IReadOnlyCollection<string> selectedProductIds)
    {
        if (selectedProductIds.Count == 0)
        {
            return products.ToList();
        }

        var selected = selectedProductIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return products
            .OrderByDescending(product => selected.Contains(product.ProductId))
            .ThenByDescending(product => product.SalesAmount)
            .ThenBy(product => product.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<LinkAttributionSummary> BuildLinkAttributionBreakdown(
        IEnumerable<SalesOrderRecord> included,
        AnalyticsSettings settings) =>
        included.SelectMany(order => order.Items.Select(item => new
            {
                item.LinkAttributionKey,
                item.LinkAttributionLabel,
                item.LinkAttributionUrl,
                order.OrderId,
                order.StoreName,
                order.BuyerHandle,
                SaleAmount = item.SalePrice,
                DiscountAmount = item.TikTokDiscountAmount,
                GrossAmount = item.SalePrice + (settings.IncludeTikTokDiscount ? item.TikTokDiscountAmount : 0m) + (settings.IncludeBuyerShippingFee && order.ItemLineCount > 0 ? order.BuyerShippingFeeAmount / order.ItemLineCount : 0m)
            }))
            .GroupBy(item => new
            {
                Key = string.IsNullOrWhiteSpace(item.LinkAttributionKey) ? UnattributedKey : item.LinkAttributionKey,
                Label = string.IsNullOrWhiteSpace(item.LinkAttributionLabel) ? UnattributedLabel : item.LinkAttributionLabel,
                Url = item.LinkAttributionUrl ?? string.Empty
            })
            .OrderByDescending(group => group.Sum(item => item.SaleAmount))
            .Select(group => new LinkAttributionSummary
            {
                Key = group.Key.Key,
                Label = group.Key.Label,
                LinkUrl = group.Key.Url,
                OrderCount = group.Select(item => item.OrderId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ItemLineCount = group.Count(),
                UniqueBuyerCount = group.Select(item => item.BuyerHandle).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                StoreCount = group.Select(item => item.StoreName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                PaidAmount = group.Sum(item => item.SaleAmount),
                TikTokDiscountAmount = group.Sum(item => item.DiscountAmount),
                GrossWithDiscount = group.Sum(item => item.GrossAmount)
            })
            .ToList();

    private static List<ReminderOrderSummary> BuildUnpaidReminders(
        IReadOnlyCollection<SalesOrderRecord> orders,
        TimeZoneInfo timezone,
        IReadOnlyDictionary<string, CustomerHistoryRecord> historyLookup) =>
        orders.Where(order => order.ExclusionReason == ExcludedUnpaid)
            .OrderByDescending(order => IsConvenienceStorePayment(order.PaymentMethod))
            .ThenByDescending(order => order.CreatedAtUtc ?? order.UpdatedAtUtc)
            .Select(order =>
            {
                var buyerKey = ResolveBuyerKey(order);
                var historyKey = ComposeHistoryKey(order.StoreKey, buyerKey);
                var isFirstObserved = !string.IsNullOrWhiteSpace(historyKey) &&
                    historyLookup.TryGetValue(historyKey, out var history) &&
                    string.Equals(history.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase);
                var createdAt = order.CreatedAtUtc ?? order.UpdatedAtUtc;
                return new ReminderOrderSummary
                {
                    StoreName = order.StoreName,
                    OrderId = order.OrderId,
                    BuyerLabel = ResolveBuyerLabel(order),
                    BuyerUserId = order.BuyerUserId,
                    BuyerEmail = order.BuyerEmail,
                    PaymentMethod = order.PaymentMethod,
                    CreatedAtLocal = FormatLocal(createdAt, timezone),
                    ExpectedAmount = order.PaidAmount,
                    IsConvenienceStorePayment = IsConvenienceStorePayment(order.PaymentMethod),
                    IsFirstObservedOrder = isFirstObserved,
                    HoursOpen = createdAt is null ? 0m : Math.Round((decimal)(DateTimeOffset.UtcNow - createdAt.Value).TotalHours, 1)
                };
            })
            .ToList();

    private static List<CustomerRiskSummary> BuildPotentialCustomers(
        IReadOnlyCollection<SalesOrderRecord> orders,
        TimeZoneInfo timezone,
        IReadOnlyDictionary<string, CustomerHistoryRecord> historyLookup) =>
        BuildFirstOrderCandidates(orders, historyLookup, order => order.ExclusionReason == ExcludedUnpaid && IsConvenienceStorePayment(order.PaymentMethod))
            .Select(order => MapCustomerRisk(order, timezone, "首单便利店未支付，建议提醒跟进"))
            .ToList();

    private static List<CustomerRiskSummary> BuildBlacklistCandidates(
        IReadOnlyCollection<SalesOrderRecord> orders,
        TimeZoneInfo timezone,
        IReadOnlyDictionary<string, CustomerHistoryRecord> historyLookup) =>
        BuildFirstOrderCandidates(orders, historyLookup, order => order.ExclusionReason == ExcludedCancelled)
            .Select(order => MapCustomerRisk(order, timezone, "首单下单后取消，可列入观察黑名单"))
            .ToList();

    private static List<SalesOrderRecord> BuildFirstOrderCandidates(
        IReadOnlyCollection<SalesOrderRecord> orders,
        IReadOnlyDictionary<string, CustomerHistoryRecord> historyLookup,
        Func<SalesOrderRecord, bool> predicate) =>
        orders.Where(predicate)
            .Where(order =>
            {
                var buyerKey = ResolveBuyerKey(order);
                if (string.IsNullOrWhiteSpace(buyerKey))
                {
                    return false;
                }

                var historyKey = ComposeHistoryKey(order.StoreKey, buyerKey);
                return !historyLookup.TryGetValue(historyKey, out var history) ||
                    string.Equals(history.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase);
            })
            .GroupBy(order => ComposeHistoryKey(order.StoreKey, ResolveBuyerKey(order)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(order => order.CreatedAtUtc ?? order.UpdatedAtUtc ?? order.PaidAtUtc).ThenBy(order => order.OrderId).First())
            .ToList();

    private static CustomerRiskSummary MapCustomerRisk(SalesOrderRecord order, TimeZoneInfo timezone, string reason)
    {
        var createdAt = order.CreatedAtUtc ?? order.UpdatedAtUtc ?? order.PaidAtUtc;
        var createdLocal = FormatLocal(createdAt, timezone);

        return new CustomerRiskSummary
        {
            BuyerLabel = ResolveBuyerLabel(order),
            BuyerUserId = order.BuyerUserId,
            BuyerEmail = order.BuyerEmail,
            StoreName = order.StoreName,
            Reason = reason,
            FirstOrderId = order.OrderId,
            FirstOrderAtLocal = createdLocal,
            TriggerOrderId = order.OrderId,
            TriggerStatus = order.Status,
            TriggerAtLocal = createdLocal,
            TriggerAmount = order.PaidAmount
        };
    }

    private static string ResolveExclusionReason(string? status, DateTimeOffset? paidAtUtc)
    {
        var normalized = status?.Trim() ?? string.Empty;
        if (normalized.Contains("CANCEL", StringComparison.OrdinalIgnoreCase))
        {
            return ExcludedCancelled;
        }

        if (normalized.Contains("REFUND", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("RETURN", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("CLOSED", StringComparison.OrdinalIgnoreCase))
        {
            return ExcludedRefunded;
        }

        if (paidAtUtc is null)
        {
            return ExcludedUnpaid;
        }

        return string.Empty;
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) ResolveRange(
        TimeZoneInfo timezone,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);
        var defaultFromDate = new DateOnly(nowLocal.Year, nowLocal.Month, 1);
        var defaultToDate = DateOnly.FromDateTime(nowLocal.DateTime);
        var from = CreateLocalBoundary(timezone, fromDate ?? defaultFromDate, false).ToUniversalTime();
        var to = CreateLocalBoundary(timezone, toDate ?? defaultToDate, true).ToUniversalTime();

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return (from, to);
    }

    private static DateTimeOffset CreateLocalBoundary(TimeZoneInfo timezone, DateOnly date, bool endOfDay)
    {
        var localDateTime = date.ToDateTime(endOfDay ? new TimeOnly(23, 59, 59) : TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localDateTime, timezone.GetUtcOffset(localDateTime));
    }

    private TimeZoneInfo ResolveTimeZone() =>
        TryGetTimeZone(_options.Timezone, out var timezone)
            ? timezone
            : TimeZoneInfo.Local;

    private static bool TryGetTimeZone(string value, out TimeZoneInfo timezone)
    {
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch
        {
            if (TimeZoneFallbacks.TryGetValue(value, out var fallback))
            {
                try
                {
                    timezone = TimeZoneInfo.FindSystemTimeZoneById(fallback);
                    return true;
                }
                catch
                {
                }
            }
        }

        timezone = TimeZoneInfo.Local;
        return false;
    }

    private string ResolveStoreLabel(string storeKey) =>
        string.Equals(storeKey, "all", StringComparison.OrdinalIgnoreCase)
            ? "全部店铺"
            : _options.Stores.FirstOrDefault(store => string.Equals(store.Key, storeKey, StringComparison.OrdinalIgnoreCase))?.Name ?? storeKey;

    private static OrderLineSummary MapOrderLine(SalesOrderRecord order, AnalyticsSettings settings, TimeZoneInfo timezone) =>
        new()
        {
            StoreName = order.StoreName,
            OrderId = order.OrderId,
            BuyerHandle = order.BuyerHandle,
            BuyerHandleSource = order.BuyerHandleSource,
            BuyerUserId = order.BuyerUserId,
            BuyerEmail = order.BuyerEmail,
            Status = order.Status,
            SettlementState = BuildSettlementState(order),
            PaymentMethod = order.PaymentMethod,
            DeliveryOptionName = order.DeliveryOptionName,
            PrimaryProductName = order.PrimaryProductName,
            LinkAttributionLabel = order.LinkAttributionLabel,
            LinkAttributionUrl = order.LinkAttributionUrl,
            PaidAmount = order.PaidAmount,
            TikTokDiscountAmount = order.TikTokDiscountAmount,
            BuyerShippingFeeAmount = order.BuyerShippingFeeAmount,
            CalculatedShippingFeeAmount = HasCalculatedShippingFee(order) ? order.BuyerShippingFeeAmount : 0m,
            EstimatedPlatformFeeAmount = settings.DeductPlatformFee && settings.PlatformFeeRate > 0m ? Math.Round(order.PaidAmount * settings.PlatformFeeRate / 100m, 2) : 0m,
            EstimatedLogisticsCostAmount = settings.DeductLogisticsCost && settings.LogisticsCostPerOrder > 0m ? settings.LogisticsCostPerOrder : 0m,
            EstimatedReceivableAmount = BuildEstimatedReceivable(order, settings),
            GrossWithDiscount = BuildDisplayedGross(order, settings),
            HasCalculatedShippingFee = HasCalculatedShippingFee(order),
            ItemLineCount = order.ItemLineCount,
            CreatedAtLocal = FormatLocal(order.CreatedAtUtc, timezone),
            PaidAtLocal = FormatLocal(order.PaidAtUtc, timezone),
            ExclusionReason = order.ExclusionReason
        };

    private static BusinessCompassAxisSummary CreateAxis(string key, string label, decimal score, string valueLabel, string description) =>
        new()
        {
            Key = key,
            Label = label,
            Score = score,
            ValueLabel = valueLabel,
            Description = description
        };

    private static decimal ClampScore(decimal value) => Math.Max(0m, Math.Min(100m, Math.Round(value, 1)));

    private static string FormatMoney(decimal value, string currency) =>
        string.Equals(currency, "JPY", StringComparison.OrdinalIgnoreCase)
            ? $"{value:N0} JPY"
            : $"{value:N2} {currency}";

    private static decimal BuildDisplayedGross(SalesOrderRecord order, AnalyticsSettings settings)
    {
        var amount = order.PaidAmount;

        if (settings.IncludeTikTokDiscount)
        {
            amount += order.TikTokDiscountAmount;
        }

        if (settings.IncludeBuyerShippingFee)
        {
            amount += order.BuyerShippingFeeAmount;
        }

        return amount;
    }

    private static string ResolveBuyerKey(SalesOrderRecord order) =>
        ResolveBuyerKey(order.BuyerHandle, order.BuyerUserId, order.BuyerEmail);

    private static string ResolveBuyerKey(string? buyerHandle, string? buyerUserId, string? buyerEmail)
    {
        if (!string.IsNullOrWhiteSpace(buyerHandle))
        {
            return $"handle:{buyerHandle.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(buyerUserId))
        {
            return $"uid:{buyerUserId.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(buyerEmail))
        {
            return $"mail:{buyerEmail.Trim()}";
        }

        return string.Empty;
    }

    private static string ResolveBuyerLabel(SalesOrderRecord order) =>
        ResolveBuyerLabel(order.BuyerHandle, order.BuyerUserId, order.BuyerEmail);

    private static string ResolveBuyerLabel(string? buyerHandle, string? buyerUserId, string? buyerEmail)
    {
        if (!string.IsNullOrWhiteSpace(buyerHandle))
        {
            return buyerHandle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(buyerUserId))
        {
            return $"UID {buyerUserId.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(buyerEmail))
        {
            return buyerEmail.Trim();
        }

        return "未识别";
    }

    private static string ComposeHistoryKey(string storeKey, string buyerKey) =>
        string.IsNullOrWhiteSpace(buyerKey)
            ? string.Empty
            : $"{storeKey}|{buyerKey}";

    private static string? ExtractString(JsonObject? node, string propertyName) =>
        node?[propertyName]?.GetValue<string?>() ?? node?[propertyName]?.ToString();

    private static decimal? ParseDecimal(JsonNode? node)
    {
        var text = node?.ToString();
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static int? ParseInt(JsonNode? node)
    {
        var text = node?.ToString();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static decimal SumDecimals(params JsonNode?[] nodes) =>
        nodes.Sum(node => ParseDecimal(node) ?? 0m);

    private static DateTimeOffset? ParseUnixSeconds(JsonNode? node)
    {
        var text = node?.ToString();
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }

    private static bool IsConvenienceStorePayment(string? paymentMethod)
    {
        var value = paymentMethod?.Trim() ?? string.Empty;
        return value.Contains("convenience", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("ConvenienceStore", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("コンビニ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSettlementCompleted(SalesOrderRecord order)
    {
        var status = order.Status?.Trim() ?? string.Empty;
        return string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "DELIVERED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCalculatedShippingFee(SalesOrderRecord order)
    {
        if (order.BuyerShippingFeeAmount <= 0m || !order.IncludedInSales)
        {
            return false;
        }

        var status = order.Status?.Trim() ?? string.Empty;
        return IsSettlementCompleted(order) ||
            status.Contains("SETTLE", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("DELIVER", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("RECEIVED", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSettlementState(SalesOrderRecord order) =>
        IsSettlementCompleted(order)
            ? "已完结 / 可视作已回款"
            : order.IncludedInSales ? "已支付待结算" : "不计入回款";

    private static decimal BuildEstimatedReceivable(SalesOrderRecord order, AnalyticsSettings settings)
    {
        var amount = order.PaidAmount;

        if (settings.IncludeTikTokDiscount)
        {
            amount += order.TikTokDiscountAmount;
        }

        if (settings.IncludeBuyerShippingFee)
        {
            amount += order.BuyerShippingFeeAmount;
        }

        if (settings.DeductPlatformFee && settings.PlatformFeeRate > 0m)
        {
            amount -= Math.Round(order.PaidAmount * settings.PlatformFeeRate / 100m, 2);
        }

        if (settings.DeductLogisticsCost && settings.LogisticsCostPerOrder > 0m)
        {
            amount -= settings.LogisticsCostPerOrder;
        }

        return amount;
    }

    private static decimal BuildReceivableWithCalculatedShipping(SalesOrderRecord order, AnalyticsSettings settings)
    {
        var amount = order.PaidAmount;

        if (settings.IncludeTikTokDiscount)
        {
            amount += order.TikTokDiscountAmount;
        }

        if (settings.DeductPlatformFee && settings.PlatformFeeRate > 0m)
        {
            amount -= Math.Round(order.PaidAmount * settings.PlatformFeeRate / 100m, 2);
        }

        var shippingAmount = HasCalculatedShippingFee(order)
            ? order.BuyerShippingFeeAmount
            : Math.Max(0m, settings.LogisticsCostPerOrder);

        amount -= shippingAmount;
        return amount;
    }

    private sealed class ProductPerformanceLine
    {
        public string ProductId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string StoreKey { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string SkuId { get; set; } = string.Empty;
        public string SkuName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal TikTokDiscountAmount { get; set; }
        public string LocalPaidDate { get; set; } = string.Empty;
        public string LocalPaidMonth { get; set; } = string.Empty;
        public decimal BuyerShippingFeeAmount { get; set; }
        public decimal CalculatedShippingFeeAmount { get; set; }
        public decimal EstimatedPlatformFeeAmount { get; set; }
        public decimal EstimatedLogisticsCostAmount { get; set; }
        public decimal EstimatedReceivableAmount { get; set; }
        public decimal EstimatedReceivableWithCalculatedShippingAmount { get; set; }
        public decimal GrossWithDiscount { get; set; }
        public bool SettlementCompleted { get; set; }
    }

    private static string FormatLocal(DateTimeOffset? timestamp, TimeZoneInfo timezone) =>
        timestamp is null
            ? string.Empty
            : TimeZoneInfo.ConvertTime(timestamp.Value, timezone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private sealed record LoadOrdersResult(
        List<SalesOrderRecord> Orders,
        Dictionary<string, CustomerHistoryRecord> HistoryLookup);

    private sealed record NormalizedLinkAttributionRule(
        string Id,
        string Label,
        string LinkUrl,
        HashSet<string> ProductIds,
        HashSet<string> SkuIds,
        HashSet<string> ProductNameKeywords);

    private sealed record CustomerHistoryRecord(
        string StoreKey,
        string OrderId,
        string BuyerKey,
        string BuyerLabel,
        string BuyerUserId,
        string BuyerEmail,
        string Status,
        DateTimeOffset CreatedAtUtc);
}
