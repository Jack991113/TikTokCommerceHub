namespace TikTokSalesStats.Options;

public sealed class SalesStatsOptions
{
    public const string SectionName = "SalesStats";

    public string Timezone { get; set; } = "Asia/Tokyo";
    public string ApiBaseUrl { get; set; } = "https://open-api.tiktokglobalshop.com";
    public string AuthBaseUrl { get; set; } = "https://auth.tiktok-shops.com";
    public string TokenRefreshPath { get; set; } = "/api/token/refreshToken";
    public int PageSize { get; set; } = 100;
    public bool AutoRefreshAccessToken { get; set; } = true;
    public string LinkAttributionRulesPath { get; set; } = "Data/link-attribution-rules.json";
    public List<StoreDataSourceOptions> Stores { get; set; } = [];
    public List<LinkAttributionRuleOptions> LinkAttributionRules { get; set; } = [];
    public List<TrackedProductOptions> TrackedProducts { get; set; } = [];
    public List<StreamerCompensationRuleOptions> StreamerCompensationRules { get; set; } = [];
}

public sealed class StoreDataSourceOptions
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuntimeStatePath { get; set; } = string.Empty;
}

public sealed class LinkAttributionRuleOptions
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

public sealed class TrackedProductOptions
{
    public string ProductId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class StreamerCompensationRuleOptions
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal BaseSalaryAmount { get; set; }
    public string BaseSalaryCurrency { get; set; } = "JPY";
    public decimal CommissionRate { get; set; }
    public string CommissionLabel { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = [];
}
