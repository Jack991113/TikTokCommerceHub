namespace TikTokOrderPrinter.Options;

public sealed class TikTokShopOptions
{
    public const string SectionName = "TikTokShop";

    public string ApiBaseUrl { get; set; } = "https://open-api.tiktokglobalshop.com";
    public string AuthBaseUrl { get; set; } = "https://auth.tiktok-shops.com";
    public string TokenExchangePath { get; set; } = "/api/token/getAccessToken";
    public string TokenRefreshPath { get; set; } = "/api/token/refreshToken";
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 30;
    public int OrderLookbackMinutes { get; set; } = 90;
    public int PageSize { get; set; } = 50;
    public bool AutoRefreshAccessToken { get; set; } = true;
    public int RefreshEarlyMinutes { get; set; } = 10;
}