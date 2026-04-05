using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TikTokSalesStats.Models;
using TikTokSalesStats.Options;

namespace TikTokSalesStats.Services;

public sealed class LinkAttributionRuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SalesStatsOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LinkAttributionRuleStore(IOptions<SalesStatsOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<IReadOnlyList<LinkAttributionRuleRecord>> GetRulesAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var path = ResolvePath();
            if (!File.Exists(path))
            {
                var defaults = NormalizeRules(_options.LinkAttributionRules.Select(MapFromOptions));
                await PersistRulesAsync(path, defaults, cancellationToken);
                return defaults;
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var loaded = JsonSerializer.Deserialize<List<LinkAttributionRuleRecord>>(json, JsonOptions) ?? [];
            var normalized = NormalizeRules(loaded);

            if (!RulesEqual(loaded, normalized))
            {
                await PersistRulesAsync(path, normalized, cancellationToken);
            }

            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LinkAttributionRuleRecord>> SaveRulesAsync(
        IEnumerable<LinkAttributionRuleRecord> rules,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRules(rules);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var path = ResolvePath();
            await PersistRulesAsync(path, normalized, cancellationToken);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ResolvePath()
    {
        var configured = _options.LinkAttributionRulesPath?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "Data/link-attribution-rules.json";
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(configured, _environment.ContentRootPath);
    }

    private static async Task PersistRulesAsync(
        string path,
        IReadOnlyList<LinkAttributionRuleRecord> rules,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(rules, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static List<LinkAttributionRuleRecord> NormalizeRules(IEnumerable<LinkAttributionRuleRecord> rules) =>
        rules.Select(rule => new LinkAttributionRuleRecord
            {
                Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id.Trim(),
                Label = rule.Label?.Trim() ?? string.Empty,
                LinkUrl = rule.LinkUrl?.Trim() ?? string.Empty,
                Enabled = rule.Enabled,
                StoreKeys = NormalizeValues(rule.StoreKeys),
                ProductIds = NormalizeValues(rule.ProductIds),
                SkuIds = NormalizeValues(rule.SkuIds),
                ProductNameKeywords = NormalizeValues(rule.ProductNameKeywords)
            })
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Label) || !string.IsNullOrWhiteSpace(rule.LinkUrl))
            .OrderBy(rule => rule.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeValues(IEnumerable<string>? values) =>
        (values ?? [])
            .SelectMany(value => (value ?? string.Empty).Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool RulesEqual(IReadOnlyList<LinkAttributionRuleRecord> left, IReadOnlyList<LinkAttributionRuleRecord> right)
    {
        var leftJson = JsonSerializer.Serialize(left, JsonOptions);
        var rightJson = JsonSerializer.Serialize(right, JsonOptions);
        return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
    }

    private static LinkAttributionRuleRecord MapFromOptions(LinkAttributionRuleOptions options) =>
        new()
        {
            Id = options.Id,
            Label = options.Label,
            LinkUrl = options.LinkUrl,
            Enabled = options.Enabled,
            StoreKeys = options.StoreKeys,
            ProductIds = options.ProductIds,
            SkuIds = options.SkuIds,
            ProductNameKeywords = options.ProductNameKeywords
        };
}
