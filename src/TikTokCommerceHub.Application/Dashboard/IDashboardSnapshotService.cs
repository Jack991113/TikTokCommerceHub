namespace TikTokCommerceHub.Application.Dashboard;

public interface IDashboardSnapshotService
{
    Task<DashboardSnapshotDto> GetAsync(CancellationToken cancellationToken);
}
