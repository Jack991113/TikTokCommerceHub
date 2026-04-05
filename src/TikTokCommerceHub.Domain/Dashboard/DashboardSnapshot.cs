namespace TikTokCommerceHub.Domain.Dashboard;

public sealed record DashboardSnapshot(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<StoreSnapshot> Stores)
{
    public int TotalOrders => Stores.Sum(item => item.TotalOrders);
    public int PaidOrders => Stores.Sum(item => item.PaidOrders);
    public int UnpaidOrders => Stores.Sum(item => item.UnpaidOrders);
    public int CancelledOrders => Stores.Sum(item => item.CancelledOrders);
    public int DeliveredOrders => Stores.Sum(item => item.DeliveredOrders);
    public int PrintedOrders => Stores.Sum(item => item.PrintedOrders);
    public int HandleCoveredOrders => Stores.Sum(item => item.HandleCoveredOrders);
}
