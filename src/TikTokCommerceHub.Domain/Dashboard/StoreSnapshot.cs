namespace TikTokCommerceHub.Domain.Dashboard;

public sealed record StoreSnapshot(
    string StoreKey,
    string StoreName,
    int TotalOrders,
    int PaidOrders,
    int UnpaidOrders,
    int CancelledOrders,
    int DeliveredOrders,
    int PrintedOrders,
    int HandleCoveredOrders,
    DateTimeOffset? LastOrderAtUtc,
    DateTimeOffset SourceFileLastWriteUtc);
