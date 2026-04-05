using TikTokCommerceHub.Domain.Dashboard;

namespace TikTokCommerceHub.Application.Abstractions;

public interface IStoreSnapshotReader
{
    Task<IReadOnlyList<StoreSnapshot>> ReadAsync(CancellationToken cancellationToken);
}
