using TikTokCommerceHub.Domain.Stores;

namespace TikTokCommerceHub.Infrastructure.Options;

public sealed class CurrentSuiteOptions
{
    public const string SectionName = "CurrentSuite";

    public List<StoreRuntimeSourceDefinition> Stores { get; set; } = [];
}
