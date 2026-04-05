using System.Text.Json;
using TikTokCommerceHub.Application.Abstractions;
using TikTokCommerceHub.Domain.Dashboard;
using TikTokCommerceHub.Infrastructure.Options;

namespace TikTokCommerceHub.Infrastructure.Dashboard;

public sealed class RuntimeStateStoreSnapshotReader : IStoreSnapshotReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CurrentSuiteOptions _options;

    public RuntimeStateStoreSnapshotReader(CurrentSuiteOptions options)
    {
        _options = options;
    }

    public async Task<IReadOnlyList<StoreSnapshot>> ReadAsync(CancellationToken cancellationToken)
    {
        var snapshots = new List<StoreSnapshot>();

        foreach (var store in _options.Stores)
        {
            if (string.IsNullOrWhiteSpace(store.RuntimeStatePath) || !File.Exists(store.RuntimeStatePath))
            {
                continue;
            }

            var json = await File.ReadAllTextAsync(store.RuntimeStatePath, cancellationToken);
            var runtime = JsonSerializer.Deserialize<RuntimeStateDocument>(json, JsonOptions) ?? new RuntimeStateDocument();
            var processedOrders = runtime.ProcessedOrders ?? [];
            var sourceInfo = new FileInfo(store.RuntimeStatePath);

            snapshots.Add(new StoreSnapshot(
                store.Key,
                string.IsNullOrWhiteSpace(runtime.StoreName) ? store.Name : runtime.StoreName,
                processedOrders.Count,
                processedOrders.Count(item => item.PaidAtUtc is not null),
                processedOrders.Count(item => item.PaidAtUtc is null && !IsCancelled(item.Status)),
                processedOrders.Count(item => IsCancelled(item.Status)),
                processedOrders.Count(item => IsDelivered(item.Status)),
                processedOrders.Count(item => item.PrintedAtUtc is not null),
                processedOrders.Count(item => !string.IsNullOrWhiteSpace(item.BuyerAccountName)),
                processedOrders
                    .Select(item => item.PaidAtUtc ?? item.CreatedAtUtc ?? item.UpdatedAtUtc)
                    .OrderByDescending(item => item)
                    .FirstOrDefault(),
                sourceInfo.LastWriteTimeUtc));
        }

        return snapshots;
    }

    private static bool IsCancelled(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        status.Contains("CANCEL", StringComparison.OrdinalIgnoreCase);

    private static bool IsDelivered(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        (status.Contains("DELIVER", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase));

    private sealed class RuntimeStateDocument
    {
        public string StoreName { get; set; } = string.Empty;
        public List<ProcessedOrderDocument>? ProcessedOrders { get; set; }
    }

    private sealed class ProcessedOrderDocument
    {
        public string Status { get; set; } = string.Empty;
        public string BuyerAccountName { get; set; } = string.Empty;
        public DateTimeOffset? CreatedAtUtc { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        public DateTimeOffset? PaidAtUtc { get; set; }
        public DateTimeOffset? PrintedAtUtc { get; set; }
    }
}
