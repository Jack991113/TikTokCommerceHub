using System.Globalization;
using TikTokOrderPrinter.Models;

namespace TikTokOrderPrinter.Services;

public static class OrderItemGrouping
{
    public static IReadOnlyList<OrderItemPrintModel> MergeLikeItems(IEnumerable<OrderItemPrintModel> items)
    {
        var grouped = new Dictionary<string, OrderItemPrintModel>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var item in items ?? [])
        {
            var key = BuildGroupKey(item);
            var quantity = GetResolvedQuantity(item);

            if (!grouped.TryGetValue(key, out var existing))
            {
                grouped[key] = Clone(item, quantity);
                order.Add(key);
                continue;
            }

            existing.Quantity = GetResolvedQuantity(existing) + quantity;
        }

        return order
            .Select(key => grouped[key])
            .ToList();
    }

    public static decimal GetResolvedQuantity(OrderItemPrintModel item)
    {
        if (item.Quantity is null || item.Quantity <= 0m)
        {
            return 1m;
        }

        return item.Quantity.Value;
    }

    public static decimal GetTotalQuantity(IEnumerable<OrderItemPrintModel> items) =>
        (items ?? []).Sum(GetResolvedQuantity);

    public static string GetDisplayTitle(OrderItemPrintModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.Sku))
        {
            return item.Sku.Trim();
        }

        return "未命名商品";
    }

    public static string GetVariantLabel(OrderItemPrintModel item) =>
        string.IsNullOrWhiteSpace(item.Variant) ? string.Empty : item.Variant.Trim();

    public static string GetDisplayTitleWithVariant(OrderItemPrintModel item, bool showVariant)
    {
        var title = GetDisplayTitle(item);
        var variant = GetVariantLabel(item);
        if (!showVariant || string.IsNullOrWhiteSpace(variant))
        {
            return title;
        }

        return $"{title}（{variant}）";
    }

    public static string FormatQuantity(decimal quantity) =>
        quantity.ToString(quantity % 1m == 0m ? "0" : "0.##", CultureInfo.InvariantCulture);

    public static string BuildCompactSummary(IReadOnlyList<OrderItemPrintModel> items)
    {
        var groupedItems = MergeLikeItems(items);
        if (groupedItems.Count == 0)
        {
            return string.Empty;
        }

        var firstItem = groupedItems[0];
        var firstTitle = GetDisplayTitleWithVariant(firstItem, showVariant: true);
        var firstQuantity = GetResolvedQuantity(firstItem);
        var firstSummary = firstQuantity > 1m
            ? $"{firstTitle} x{FormatQuantity(firstQuantity)}"
            : firstTitle;

        if (groupedItems.Count == 1)
        {
            return firstSummary;
        }

        return $"{firstSummary}，另 {groupedItems.Count - 1} 款";
    }

    private static string BuildGroupKey(OrderItemPrintModel item)
    {
        var title = Normalize(GetDisplayTitle(item));
        var variant = Normalize(item.Variant);
        var sku = Normalize(item.Sku);
        var price = item.UnitPrice?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{title}\u001f{variant}\u001f{sku}\u001f{price}";
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static OrderItemPrintModel Clone(OrderItemPrintModel item, decimal quantity) =>
        new()
        {
            Title = item.Title,
            Variant = item.Variant,
            Sku = item.Sku,
            Quantity = quantity,
            UnitPrice = item.UnitPrice
        };
}
