namespace TikTokSalesStats.Models;

public sealed class RuntimeStateSnapshot
{
    public string StoreName { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }
    public List<ProcessedOrderRecord> ProcessedOrders { get; set; } = [];
}

public sealed class ProcessedOrderRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string BuyerAccountName { get; set; } = string.Empty;
    public DateTimeOffset? BuyerAccountNameCapturedAtUtc { get; set; }
    public string BuyerPlatformUserId { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public string PayloadFilePath { get; set; } = string.Empty;
}
