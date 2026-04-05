namespace TikTokSalesStats.Models;

public sealed class SalesItemRecord
{
    public string ProductId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal SalePrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public string LinkAttributionKey { get; set; } = string.Empty;
    public string LinkAttributionLabel { get; set; } = string.Empty;
    public string LinkAttributionUrl { get; set; } = string.Empty;
    public string DisplayName =>
        string.IsNullOrWhiteSpace(SkuName)
            ? ProductName
            : $"{ProductName} / {SkuName}";
}

public sealed class SalesOrderRecord
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string BuyerHandle { get; set; } = string.Empty;
    public string BuyerHandleSource { get; set; } = string.Empty;
    public string BuyerUserId { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string DeliveryOptionName { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal OriginalShippingFeeAmount { get; set; }
    public decimal SellerDiscountAmount { get; set; }
    public decimal ShippingSellerDiscountAmount { get; set; }
    public decimal ShippingPlatformDiscountAmount { get; set; }
    public decimal GrossWithTikTokDiscount => PaidAmount + TikTokDiscountAmount;
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? RangeAnchorUtc { get; set; }
    public string LocalPaidDate { get; set; } = string.Empty;
    public string LocalPaidMonth { get; set; } = string.Empty;
    public int? LocalPaidHour { get; set; }
    public bool IncludedInSales { get; set; }
    public string ExclusionReason { get; set; } = string.Empty;
    public string LinkAttributionKey { get; set; } = string.Empty;
    public string LinkAttributionLabel { get; set; } = string.Empty;
    public string LinkAttributionUrl { get; set; } = string.Empty;
    public List<SalesItemRecord> Items { get; set; } = [];
    public int ItemLineCount => Items.Count;
    public string PrimaryProductName => Items.FirstOrDefault()?.DisplayName ?? string.Empty;
}

public sealed class SalesSummaryResponse
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Currency { get; set; } = "JPY";
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public AnalyticsSettings Settings { get; set; } = new();
    public OverviewSummary Overview { get; set; } = new();
    public FunnelSummary Funnel { get; set; } = new();
    public BusinessCompassSummary BusinessCompass { get; set; } = new();
    public SalesInsightsSummary Insights { get; set; } = new();
    public ReconciliationSummary Reconciliation { get; set; } = new();
    public List<MonthlySummary> Monthly { get; set; } = [];
    public List<DailySummary> Daily { get; set; } = [];
    public List<HourlySummary> Hourly { get; set; } = [];
    public List<StoreBreakdownSummary> StoreBreakdown { get; set; } = [];
    public List<PaymentBreakdownSummary> PaymentBreakdown { get; set; } = [];
    public List<StatusBreakdownSummary> StatusBreakdown { get; set; } = [];
    public List<BuyerBreakdownSummary> TopBuyers { get; set; } = [];
    public List<BuyerBreakdownSummary> PaidBuyerRanking { get; set; } = [];
    public List<ProductBreakdownSummary> TopProducts { get; set; } = [];
    public List<string> SelectedProductIds { get; set; } = [];
    public List<ProductIdBreakdownSummary> ProductIdBreakdown { get; set; } = [];
    public List<LinkAttributionSummary> LinkAttributionBreakdown { get; set; } = [];
    public List<ReminderOrderSummary> UnpaidReminders { get; set; } = [];
    public List<CustomerRiskSummary> PotentialCustomers { get; set; } = [];
    public List<CustomerRiskSummary> BlacklistCandidates { get; set; } = [];
    public SummaryDerivedMetrics DerivedMetrics { get; set; } = new();
    public int IncludedOrderTotalCount { get; set; }
    public int ExcludedOrderTotalCount { get; set; }
    public bool IncludedOrdersTruncated { get; set; }
    public bool ExcludedOrdersTruncated { get; set; }
    public List<OrderLineSummary> TopOrders { get; set; } = [];
    public List<OrderLineSummary> IncludedOrders { get; set; } = [];
    public List<OrderLineSummary> ExcludedOrders { get; set; } = [];
}

public sealed class SummaryDerivedMetrics
{
    public List<OrderBucketSummary> OrderBuckets { get; set; } = [];
    public List<HandleSourceSummary> HandleSources { get; set; } = [];
    public List<BuyerSegmentSummary> BuyerSegments { get; set; } = [];
    public List<SettlementRowSummary> SettlementRows { get; set; } = [];
    public List<OrderLineSummary> DiscountOrders { get; set; } = [];
}

public sealed class OrderBucketSummary
{
    public string Label { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class HandleSourceSummary
{
    public string Label { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class BuyerSegmentSummary
{
    public string Label { get; set; } = string.Empty;
    public int BuyerCount { get; set; }
    public int OrderCount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class SettlementRowSummary
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int OrderCount { get; set; }
}

public sealed class TrackedProductsResponse
{
    public string Timezone { get; set; } = string.Empty;
    public List<TrackedProductDefinition> TrackedProducts { get; set; } = [];
}

public sealed class ProductPerformanceResponse
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Currency { get; set; } = "JPY";
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public AnalyticsSettings Settings { get; set; } = new();
    public ProductPerformanceTotalsSummary Totals { get; set; } = new();
    public List<TrackedProductDefinition> TrackedProducts { get; set; } = [];
    public List<ProductPerformanceSummary> Products { get; set; } = [];
}

public sealed class StreamerCompensationResponse
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Currency { get; set; } = "JPY";
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public AnalyticsSettings Settings { get; set; } = new();
    public decimal CnyToJpyRate { get; set; }
    public decimal HiddenProcurementCostJpy { get; set; }
    public StreamerCompensationTotalsSummary Totals { get; set; } = new();
    public List<StreamerCompensationSummary> Streamers { get; set; } = [];
    public StreamerCompensationSummary SelfOwned { get; set; } = new();
    public List<StreamerMonthlyProfitSummary> MonthlyProfit { get; set; } = [];
}

public sealed class TrackedProductDefinition
{
    public string ProductId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class StreamerRuleDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal BaseSalaryAmount { get; set; }
    public string BaseSalaryCurrency { get; set; } = "RMB";
    public decimal CommissionRate { get; set; }
    public string CommissionLabel { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = [];
}

public sealed class StreamerCompensationOverride
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = [];
    public decimal? BaseSalaryAmount { get; set; }
    public decimal? CommissionRate { get; set; }
}

public sealed class AnalyticsSettings
{
    public bool IncludeTikTokDiscount { get; set; } = true;
    public bool IncludeBuyerShippingFee { get; set; }
    public bool DeductPlatformFee { get; set; } = true;
    public bool DeductLogisticsCost { get; set; } = true;
    public decimal PlatformFeeRate { get; set; }
    public decimal LogisticsCostPerOrder { get; set; }
}

public sealed class FunnelSummary
{
    public int ObservedOrderCount { get; set; }
    public int IncludedOrderCount { get; set; }
    public int AwaitingCollectionStatusCount { get; set; }
    public int ExcludedCancelledCount { get; set; }
    public int ExcludedUnpaidCount { get; set; }
    public int ExcludedRefundedCount { get; set; }
}

public sealed class BusinessCompassSummary
{
    public decimal OverallScore { get; set; }
    public List<BusinessCompassAxisSummary> Axes { get; set; } = [];
}

public sealed class BusinessCompassAxisSummary
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string ValueLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class SalesInsightsSummary
{
    public InsightMetricSummary BestDay { get; set; } = new();
    public InsightMetricSummary BestHour { get; set; } = new();
    public InsightMetricSummary BestStore { get; set; } = new();
    public InsightMetricSummary BestPayment { get; set; } = new();
    public InsightMetricSummary BestBuyer { get; set; } = new();
    public InsightMetricSummary BestProduct { get; set; } = new();
}

public sealed class ReconciliationSummary
{
    public decimal BasePaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedShippingFeeAmount { get; set; }
    public decimal ActualShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedSettledReceivableAmount { get; set; }
    public decimal EstimatedPendingReceivableAmount { get; set; }
    public decimal EstimatedReceivableAfterEstimatedShippingAmount { get; set; }
    public decimal ActualReceivableAfterActualShippingAmount { get; set; }
    public decimal ActualSettledReceivableAfterActualShippingAmount { get; set; }
    public decimal ActualPendingReceivableAfterActualShippingAmount { get; set; }
    public decimal SettledActualShippingFeeAmount { get; set; }
    public decimal SettledAverageShippingFeeAmount { get; set; }
    public decimal SettlementCompletionRate { get; set; }
    public int CalculatedShippingOrderCount { get; set; }
    public int CompletedOrderCount { get; set; }
    public int ReconcilableOrderCount { get; set; }
    public int PendingSettlementOrderCount { get; set; }
    public int EstimatedShippingOrderCount { get; set; }
    public int ActualShippingFallbackOrderCount { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class InsightMetricSummary
{
    public string Title { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed class OverviewSummary
{
    public int ObservedOrderCount { get; set; }
    public int IncludedOrderCount { get; set; }
    public decimal IncludedPaidAmount { get; set; }
    public decimal IncludedTikTokDiscountAmount { get; set; }
    public decimal IncludedGrossWithDiscount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int UniqueBuyerCount { get; set; }
    public int RepeatBuyerCount { get; set; }
    public decimal RepeatBuyerRate { get; set; }
    public decimal HandleCoverageRate { get; set; }
    public decimal ValidOrderRate { get; set; }
    public int ExcludedTotalCount { get; set; }
    public int AwaitingCollectionStatusCount { get; set; }
    public int ExcludedCancelledCount { get; set; }
    public int ExcludedUnpaidCount { get; set; }
    public int ExcludedRefundedCount { get; set; }
}

public sealed class ProductPerformanceTotalsSummary
{
    public int ProductCount { get; set; }
    public int OrderCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedSettledReceivableAmount { get; set; }
    public decimal EstimatedPendingReceivableAmount { get; set; }
    public decimal SettlementCompletionRate { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingSettlementOrderCount { get; set; }
}

public sealed class StreamerCompensationTotalsSummary
{
    public int ProductCount { get; set; }
    public int OrderCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedSettledReceivableAmount { get; set; }
    public decimal EstimatedPendingReceivableAmount { get; set; }
    public decimal EstimatedReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedSettledReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedPendingReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedReceivableWithCalculatedShippingAmount { get; set; }
    public decimal EstimatedSettledReceivableWithCalculatedShippingAmount { get; set; }
    public decimal EstimatedPendingReceivableWithCalculatedShippingAmount { get; set; }
    public decimal SalaryBaseAmountRmb { get; set; }
    public decimal SalaryCommissionAmountRmb { get; set; }
    public decimal SalaryTotalAmountRmb { get; set; }
    public decimal SalaryBaseAmountJpy { get; set; }
    public decimal SalaryCommissionAmountJpy { get; set; }
    public decimal SalaryTotalAmountJpy { get; set; }
    public decimal HiddenProcurementCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostJpy { get; set; }
    public decimal ProfitAfterHiddenCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitAfterHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithCalculatedShippingJpy { get; set; }
    public decimal ProfitAfterHiddenCostWithCalculatedShippingJpy { get; set; }
    public decimal SettlementCompletionRate { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingSettlementOrderCount { get; set; }
}

public sealed class MonthlySummary
{
    public string Month { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class DailySummary
{
    public string Date { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class HourlySummary
{
    public string HourLabel { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
}

public sealed class StoreBreakdownSummary
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int UniqueBuyerCount { get; set; }
}

public sealed class PaymentBreakdownSummary
{
    public string PaymentMethod { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public decimal PaidAmountShareRate { get; set; }
}

public sealed class StatusBreakdownSummary
{
    public string Status { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
}

public sealed class BuyerBreakdownSummary
{
    public string BuyerLabel { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public int StoreCount { get; set; }
}

public sealed class ReminderOrderSummary
{
    public string StoreName { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string BuyerLabel { get; set; } = string.Empty;
    public string BuyerUserId { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string CreatedAtLocal { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public bool IsConvenienceStorePayment { get; set; }
    public bool IsFirstObservedOrder { get; set; }
    public decimal HoursOpen { get; set; }
}

public sealed class CustomerRiskSummary
{
    public string BuyerLabel { get; set; } = string.Empty;
    public string BuyerUserId { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string FirstOrderId { get; set; } = string.Empty;
    public string FirstOrderAtLocal { get; set; } = string.Empty;
    public string TriggerOrderId { get; set; } = string.Empty;
    public string TriggerStatus { get; set; } = string.Empty;
    public string TriggerAtLocal { get; set; } = string.Empty;
    public decimal TriggerAmount { get; set; }
}

public sealed class ProductBreakdownSummary
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int Quantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class ProductIdBreakdownSummary
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int Quantity { get; set; }
    public int SkuCount { get; set; }
    public int StoreCount { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public List<ProductIdStoreBreakdownSummary> StoreBreakdown { get; set; } = [];
    public List<ProductSkuBreakdownSummary> SkuBreakdown { get; set; } = [];
}

public sealed class ProductPerformanceSummary
{
    public string ProductId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int Quantity { get; set; }
    public int SkuCount { get; set; }
    public int StoreCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedSettledReceivableAmount { get; set; }
    public decimal EstimatedPendingReceivableAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public decimal SettlementCompletionRate { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingSettlementOrderCount { get; set; }
    public List<ProductPerformanceMonthlySummary> Monthly { get; set; } = [];
    public List<ProductPerformanceDailySummary> Daily { get; set; } = [];
    public List<ProductPerformanceStoreSummary> StoreBreakdown { get; set; } = [];
}

public sealed class StreamerCompensationSummary
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsSelfOwned { get; set; }
    public decimal BaseSalaryAmount { get; set; }
    public string BaseSalaryCurrency { get; set; } = "RMB";
    public decimal CommissionAmountRmb { get; set; }
    public decimal SalaryTotalAmountRmb { get; set; }
    public decimal BaseSalaryAmountJpy { get; set; }
    public decimal CommissionRate { get; set; }
    public string CommissionLabel { get; set; } = string.Empty;
    public decimal CommissionAmountJpy { get; set; }
    public decimal SalaryTotalAmountJpy { get; set; }
    public int ProductCount { get; set; }
    public int OrderCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedSettledReceivableAmount { get; set; }
    public decimal EstimatedPendingReceivableAmount { get; set; }
    public decimal EstimatedReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedSettledReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedPendingReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedReceivableWithCalculatedShippingAmount { get; set; }
    public decimal EstimatedSettledReceivableWithCalculatedShippingAmount { get; set; }
    public decimal EstimatedPendingReceivableWithCalculatedShippingAmount { get; set; }
    public decimal AllocatedHiddenProcurementCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostJpy { get; set; }
    public decimal ProfitAfterHiddenCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitAfterHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithCalculatedShippingJpy { get; set; }
    public decimal ProfitAfterHiddenCostWithCalculatedShippingJpy { get; set; }
    public decimal SettlementCompletionRate { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingSettlementOrderCount { get; set; }
    public List<TrackedProductDefinition> ProductIds { get; set; } = [];
    public List<StreamerCompensationMonthlySummary> Monthly { get; set; } = [];
    public List<ProductPerformanceStoreSummary> StoreBreakdown { get; set; } = [];
}

public sealed class StreamerCompensationMonthlySummary
{
    public string Month { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedReceivableWithCalculatedShippingAmount { get; set; }
    public decimal BaseSalaryAmount { get; set; }
    public string BaseSalaryCurrency { get; set; } = "RMB";
    public decimal CommissionAmountRmb { get; set; }
    public decimal SalaryTotalAmountRmb { get; set; }
    public decimal BaseSalaryAmountJpy { get; set; }
    public decimal CommissionAmountJpy { get; set; }
    public decimal SalaryTotalAmountJpy { get; set; }
    public decimal ProfitBeforeHiddenCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithCalculatedShippingJpy { get; set; }
}

public sealed class StreamerMonthlyProfitSummary
{
    public string Month { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedReceivableWithEstimatedLogisticsAmount { get; set; }
    public decimal EstimatedReceivableWithCalculatedShippingAmount { get; set; }
    public decimal SalaryBaseAmountRmb { get; set; }
    public decimal SalaryCommissionAmountRmb { get; set; }
    public decimal SalaryTotalAmountRmb { get; set; }
    public decimal SalaryBaseAmountJpy { get; set; }
    public decimal SalaryCommissionAmountJpy { get; set; }
    public decimal SalaryTotalAmountJpy { get; set; }
    public decimal HiddenProcurementCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostJpy { get; set; }
    public decimal ProfitAfterHiddenCostJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitAfterHiddenCostWithEstimatedLogisticsJpy { get; set; }
    public decimal ProfitBeforeHiddenCostWithCalculatedShippingJpy { get; set; }
    public decimal ProfitAfterHiddenCostWithCalculatedShippingJpy { get; set; }
}

public sealed class ProductPerformanceStoreSummary
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal EstimatedSettledReceivableAmount { get; set; }
    public decimal EstimatedPendingReceivableAmount { get; set; }
    public decimal EstimatedReceivableWithCalculatedShippingAmount { get; set; }
    public decimal EstimatedSettledReceivableWithCalculatedShippingAmount { get; set; }
    public decimal EstimatedPendingReceivableWithCalculatedShippingAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public decimal SettlementCompletionRate { get; set; }
    public int CompletedOrderCount { get; set; }
    public int PendingSettlementOrderCount { get; set; }
}

public sealed class ProductPerformanceMonthlySummary
{
    public string Month { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class ProductPerformanceDailySummary
{
    public string Date { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int Quantity { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class ProductIdStoreBreakdownSummary
{
    public string StoreKey { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int Quantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class ProductSkuBreakdownSummary
{
    public string ProductId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int Quantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class LinkAttributionSummary
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ItemLineCount { get; set; }
    public int UniqueBuyerCount { get; set; }
    public int StoreCount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
}

public sealed class OrderLineSummary
{
    public string StoreName { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string BuyerHandle { get; set; } = string.Empty;
    public string BuyerHandleSource { get; set; } = string.Empty;
    public string BuyerUserId { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SettlementState { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string DeliveryOptionName { get; set; } = string.Empty;
    public string PrimaryProductName { get; set; } = string.Empty;
    public string LinkAttributionLabel { get; set; } = string.Empty;
    public string LinkAttributionUrl { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public decimal TikTokDiscountAmount { get; set; }
    public decimal BuyerShippingFeeAmount { get; set; }
    public decimal CalculatedShippingFeeAmount { get; set; }
    public decimal EstimatedPlatformFeeAmount { get; set; }
    public decimal EstimatedLogisticsCostAmount { get; set; }
    public decimal EstimatedReceivableAmount { get; set; }
    public decimal GrossWithDiscount { get; set; }
    public bool HasCalculatedShippingFee { get; set; }
    public int ItemLineCount { get; set; }
    public string CreatedAtLocal { get; set; } = string.Empty;
    public string PaidAtLocal { get; set; } = string.Empty;
    public string ExclusionReason { get; set; } = string.Empty;
}

public sealed class LinkAttributionRuleRecord
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> StoreKeys { get; set; } = [];
    public List<string> ProductIds { get; set; } = [];
    public List<string> SkuIds { get; set; } = [];
    public List<string> ProductNameKeywords { get; set; } = [];
}

public sealed class LinkAttributionRulesResponse
{
    public List<LinkAttributionRuleRecord> Rules { get; set; } = [];
}
