using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TikTokOrderPrinter.Models;
using TikTokOrderPrinter.Options;

namespace TikTokOrderPrinter.Services;

public sealed class OrderTicketRenderer
{
    private const string DefaultTemplate = """
店铺：{{store_name}}
订单号：{{order_id}}
客户ID：{{buyer_handle}}
金额：{{amount}}
购买时间：{{purchase_time}}
客户备注：{{buyer_message}}
{{items}}
""";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly RuntimeStateStore _stateStore;
    private readonly string _printJobDirectory;
    private readonly string _ticketTemplatePath;
    private readonly PrintingOptions _printingOptions;

    public OrderTicketRenderer(
        RuntimeStateStore stateStore,
        IOptions<AppDataOptions> appDataOptions,
        IOptions<PrintingOptions> printingOptions)
    {
        _stateStore = stateStore;
        _printJobDirectory = Path.GetFullPath(appDataOptions.Value.PrintJobDirectory);
        _ticketTemplatePath = Path.GetFullPath(appDataOptions.Value.TicketTemplatePath);
        _printingOptions = printingOptions.Value;
    }

    public async Task<OrderPrintArtifacts> CreateArtifactsAsync(OrderPrintModel order, JsonObject rawPayload, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_printJobDirectory);

        var ticketContent = await BuildTicketContentAsync(order, rawPayload, cancellationToken);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safeOrderId = string.Concat(order.OrderId.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        var baseFileName = $"{timestamp}-{safeOrderId}";
        var ticketPath = Path.Combine(_printJobDirectory, $"{baseFileName}.txt");
        var payloadPath = Path.Combine(_printJobDirectory, $"{baseFileName}.json");

        if (_printingOptions.SaveArtifacts)
        {
            await File.WriteAllTextAsync(ticketPath, ticketContent, cancellationToken);
            await File.WriteAllTextAsync(payloadPath, rawPayload.ToJsonString(JsonOptions), cancellationToken);
        }

        return new OrderPrintArtifacts
        {
            OrderId = order.OrderId,
            TicketFilePath = ticketPath,
            PayloadFilePath = payloadPath,
            TicketContent = ticketContent
        };
    }

    public Task<string> BuildTicketContentAsync(OrderPrintModel order, CancellationToken cancellationToken) =>
        BuildTicketContentAsync(order, rawPayload: null, cancellationToken);

    public async Task<string> BuildTicketContentAsync(OrderPrintModel order, JsonObject? rawPayload, CancellationToken cancellationToken)
    {
        var state = await _stateStore.GetSnapshotAsync(cancellationToken);
        var storeName = string.IsNullOrWhiteSpace(state.StoreName) ? "TikTok Shop" : state.StoreName.Trim();
        var profile = LabelPaperProfiles.Resolve(
            string.IsNullOrWhiteSpace(state.PaperSize) ? _printingOptions.PaperSize : state.PaperSize,
            state.CustomPaperWidthMm ?? _printingOptions.CustomPaperWidthMm,
            state.CustomPaperHeightMm ?? _printingOptions.CustomPaperHeightMm,
            state.PaperWidthCharacters ?? _printingOptions.PaperWidthCharacters,
            state.BaseFontSize ?? _printingOptions.BaseFontSize,
            state.MinFontSize ?? _printingOptions.MinFontSize,
            state.MarginMm ?? _printingOptions.MarginMm);

        var template = await LoadTemplateAsync(cancellationToken);
        return RenderTemplate(
            template,
            order,
            storeName,
            profile.ContentWidthCharacters,
            state.ShowBuyerMessage ?? _printingOptions.ShowBuyerMessage,
            state.ShowOrderAmounts ?? _printingOptions.ShowOrderAmounts,
            state.ShowItemDetails ?? _printingOptions.ShowItemDetails,
            state.ShowSku ?? _printingOptions.ShowSku);
    }

    private async Task<string> LoadTemplateAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_ticketTemplatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_ticketTemplatePath))
        {
            await File.WriteAllTextAsync(_ticketTemplatePath, DefaultTemplate, cancellationToken);
            return DefaultTemplate;
        }

        var template = await File.ReadAllTextAsync(_ticketTemplatePath, cancellationToken);
        return string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
    }

    private static string RenderTemplate(
        string template,
        OrderPrintModel order,
        string storeName,
        int width,
        bool showBuyerMessage,
        bool showOrderAmounts,
        bool showItemDetails,
        bool showSku)
    {
        var groupedItems = OrderItemGrouping.MergeLikeItems(order.Items);
        var buyerHandle = string.IsNullOrWhiteSpace(order.BuyerAccountName) ? "未桥接" : order.BuyerAccountName.Trim();
        var itemsBlock = BuildItemsBlock(groupedItems, showItemDetails, showSku);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{store_name}}"] = storeName,
            ["{{order_id}}"] = order.OrderId,
            ["{{buyer_handle}}"] = buyerHandle,
            ["{{amount}}"] = showOrderAmounts ? FormatAmount(order.TotalAmount, order.Currency) : string.Empty,
            ["{{purchase_time}}"] = FormatDate(order.PaidAtUtc ?? order.CreatedAtUtc),
            ["{{buyer_message}}"] = showBuyerMessage
                ? (string.IsNullOrWhiteSpace(order.BuyerMessage) ? "无" : order.BuyerMessage.Trim())
                : string.Empty,
            ["{{items}}"] = itemsBlock,
            ["{{item_details}}"] = itemsBlock,
            ["{{items_summary}}"] = OrderItemGrouping.BuildCompactSummary(order.Items),
            ["{{currency}}"] = order.Currency?.Trim() ?? string.Empty,
            ["{{status}}"] = order.Status?.Trim() ?? string.Empty
        };

        var rendered = template.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var entry in values)
        {
            rendered = rendered.Replace(entry.Key, entry.Value, StringComparison.OrdinalIgnoreCase);
        }

        return WrapRenderedText(rendered, width);
    }

    private static string BuildItemsBlock(IReadOnlyList<OrderItemPrintModel> groupedItems, bool showItemDetails, bool showSku)
    {
        if (!showItemDetails || groupedItems.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(groupedItems.Count);
        for (var index = 0; index < groupedItems.Count; index += 1)
        {
            var item = groupedItems[index];
            var title = OrderItemGrouping.GetDisplayTitle(item);
            var variant = showSku ? ResolveVariantLabel(item) : string.Empty;
            var quantity = OrderItemGrouping.FormatQuantity(OrderItemGrouping.GetResolvedQuantity(item));
            var line = $"商品{index + 1}：{title}";

            if (!string.IsNullOrWhiteSpace(variant))
            {
                line += $" / SKU:{variant}";
            }

            line += $" x{quantity}";
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ResolveVariantLabel(OrderItemPrintModel item)
    {
        var variant = OrderItemGrouping.GetVariantLabel(item);
        if (!string.IsNullOrWhiteSpace(variant))
        {
            return variant;
        }

        if (!string.IsNullOrWhiteSpace(item.Sku) && LooksLikeVariantLabel(item.Sku))
        {
            return item.Sku.Trim();
        }

        return string.Empty;
    }

    private static string WrapRenderedText(string text, int width)
    {
        var builder = new StringBuilder();
        var lines = text.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine();
                continue;
            }

            AppendWrappedLine(builder, line, width);
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendWrappedLine(StringBuilder builder, string line, int width)
    {
        var remaining = line.Trim();
        while (!string.IsNullOrEmpty(remaining))
        {
            var availableWidth = Math.Max(8, width);
            var take = Math.Min(availableWidth, remaining.Length);
            var chunk = remaining[..take];

            if (take < remaining.Length)
            {
                var split = chunk.LastIndexOf(' ');
                if (split > 8)
                {
                    take = split;
                    chunk = remaining[..take];
                }
            }

            builder.AppendLine(chunk.TrimEnd());
            remaining = remaining[take..].TrimStart();
        }
    }

    private static string FormatAmount(decimal? amount, string currency)
    {
        if (amount is null)
        {
            return string.Empty;
        }

        var amountValue = amount.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(currency) ? amountValue : $"{amountValue} {currency}";
    }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool LooksLikeVariantLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Any(char.IsDigit) &&
               (trimmed.Contains('*') || trimmed.Contains('×') || trimmed.Contains('x') || trimmed.Contains('X'));
    }
}
