using TikTokCommerceHub.Application.Abstractions;
using TikTokCommerceHub.Domain.Dashboard;

namespace TikTokCommerceHub.Application.Dashboard;

public sealed class DashboardSnapshotService : IDashboardSnapshotService
{
    private readonly IStoreSnapshotReader _storeSnapshotReader;
    private readonly TimeProvider _timeProvider;

    public DashboardSnapshotService(IStoreSnapshotReader storeSnapshotReader, TimeProvider timeProvider)
    {
        _storeSnapshotReader = storeSnapshotReader;
        _timeProvider = timeProvider;
    }

    public async Task<DashboardSnapshotDto> GetAsync(CancellationToken cancellationToken)
    {
        var stores = await _storeSnapshotReader.ReadAsync(cancellationToken);
        var snapshot = new DashboardSnapshot(_timeProvider.GetUtcNow(), stores);

        return new DashboardSnapshotDto(
            snapshot.GeneratedAtUtc,
            new DashboardTotalsDto(
                snapshot.TotalOrders,
                snapshot.PaidOrders,
                snapshot.UnpaidOrders,
                snapshot.CancelledOrders,
                snapshot.DeliveredOrders,
                snapshot.PrintedOrders,
                snapshot.HandleCoveredOrders),
            snapshot.Stores
                .OrderBy(item => item.StoreName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new StoreSnapshotDto(
                    item.StoreKey,
                    item.StoreName,
                    item.TotalOrders,
                    item.PaidOrders,
                    item.UnpaidOrders,
                    item.CancelledOrders,
                    item.DeliveredOrders,
                    item.PrintedOrders,
                    item.HandleCoveredOrders,
                    item.LastOrderAtUtc,
                    item.SourceFileLastWriteUtc))
                .ToList(),
            "新架构只保留读模型、聚合服务和轻量 API。高频看板先读本地状态快照，后续再把 TikTok API 同步迁移进来。");
    }
}
