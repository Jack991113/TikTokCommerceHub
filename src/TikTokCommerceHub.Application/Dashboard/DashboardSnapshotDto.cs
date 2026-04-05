namespace TikTokCommerceHub.Application.Dashboard;

public sealed record DashboardSnapshotDto(
    DateTimeOffset GeneratedAtUtc,
    DashboardTotalsDto Totals,
    IReadOnlyList<StoreSnapshotDto> Stores,
    string ArchitectureNote);

public sealed record DashboardTotalsDto(
    int TotalOrders,
    int PaidOrders,
    int UnpaidOrders,
    int CancelledOrders,
    int DeliveredOrders,
    int PrintedOrders,
    int HandleCoveredOrders);

public sealed record StoreSnapshotDto(
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
