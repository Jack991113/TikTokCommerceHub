using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using TikTokOrderPrinter.Models;

namespace TikTokOrderPrinter.Services;

public sealed class OrderPayloadMapper
{
    public IReadOnlyList<string> ExtractOrderIds(JsonObject response)
    {
        if (TryGetByPath(response, "data.orders") is JsonArray ordersArray)
        {
            return ordersArray
                .OfType<JsonObject>()
                .Select(order => ExtractString(order, "order_id", "id"))
                .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectOrderIds(response, results);
        return results.ToList();
    }

    public OrderPrintModel MapOrder(JsonObject response)
    {
        var orderObject = FindFirstOrderObject(response)
            ?? throw new InvalidOperationException("Could not find an order payload in the TikTok detail response.");

        var recipientName = ExtractString(orderObject,
            "recipient_address.name",
            "shipping_address.name",
            "delivery_address.name",
            "receiver_name",
            "recipient_name");

        if (string.IsNullOrWhiteSpace(recipientName))
        {
            recipientName = JoinName(
                ExtractString(orderObject, "recipient_address.first_name", "shipping_address.first_name", "delivery_address.first_name"),
                ExtractString(orderObject, "recipient_address.last_name", "shipping_address.last_name", "delivery_address.last_name"));
        }

        var buyerAccountName = ExtractStringStrict(orderObject,
            "buyer_nickname",
            "buyer.nickname",
            "nickname",
            "buyer_username",
            "buyer_user_name",
            "buyer_handle",
            "buyer_account_name",
            "buyer_account",
            "buyer.handle",
            "buyer.username",
            "buyer.user_name",
            "user_name",
            "username",
            "handle");

        var buyerPlatformUserId = ExtractStringStrict(orderObject,
            "buyer_user_id",
            "buyer_open_id",
            "buyer_platform_user_id",
            "user_id");

        var buyerEmail = ExtractStringStrict(orderObject,
            "buyer_email",
            "buyer.email",
            "customer_email");

        var buyerName = ExtractStringStrict(orderObject,
            "buyer_name",
            "buyer_display_name",
            "buyer_displayname",
            "display_name",
            "buyer.display_name",
            "customer_name");

        var order = new OrderPrintModel
        {
            OrderId = ExtractString(orderObject, "order_id", "id"),
            Status = ExtractString(orderObject, "order_status", "status", "order_state"),
            CreatedAtUtc = ExtractDateTime(orderObject, "create_time", "created_at"),
            UpdatedAtUtc = ExtractDateTime(orderObject, "update_time", "updated_at"),
            PaidAtUtc = ExtractDateTime(orderObject, "paid_time", "pay_time", "payment_time", "payment_info.paid_time"),
            BuyerAccountName = buyerAccountName,
            BuyerPlatformUserId = buyerPlatformUserId,
            BuyerName = buyerName,
            BuyerEmail = buyerEmail,
            RecipientName = recipientName,
            RecipientPhone = ExtractString(orderObject,
                "recipient_address.phone_number",
                "recipient_address.phone",
                "shipping_address.phone",
                "delivery_address.phone",
                "receiver_phone",
                "recipient_phone",
                "phone"),
            RecipientAddress = BuildAddress(orderObject),
            BuyerMessage = ExtractString(orderObject, "buyer_message", "message_to_seller", "seller_note"),
            TotalAmount = ExtractDecimal(orderObject,
                "payment_info.total_amount",
                "payment.total_amount",
                "total_amount",
                "order_amount"),
            Currency = ExtractString(orderObject,
                "payment_info.currency",
                "payment.currency",
                "currency",
                "currency_code")
        };

        order.Items = ExtractItems(orderObject);
        if (string.IsNullOrWhiteSpace(order.OrderId))
        {
            order.OrderId = "unknown-order";
        }

        return order;
    }

    public OrderListItem MapListItem(JsonObject response, string source, PrintedOrderRecord? cachedRecord = null)
    {
        var order = MapOrder(response);
        var groupedItems = OrderItemGrouping.MergeLikeItems(order.Items);
        var hasLocalPayload = cachedRecord is not null &&
                              !string.IsNullOrWhiteSpace(cachedRecord.PayloadFilePath) &&
                              File.Exists(cachedRecord.PayloadFilePath);

        return new OrderListItem
        {
            OrderId = order.OrderId,
            Source = source,
            IsCached = cachedRecord is not null,
            HasLocalPayload = hasLocalPayload,
            DisplayName = order.DisplayName,
            BuyerAccountName = FirstNonEmpty(cachedRecord?.BuyerAccountName, order.BuyerAccountName),
            BuyerAccountNameSource = cachedRecord?.BuyerAccountNameSource ?? string.Empty,
            BuyerAccountNameCapturedAtUtc = cachedRecord?.BuyerAccountNameCapturedAtUtc,
            BuyerPlatformUserId = FirstNonEmpty(cachedRecord?.BuyerPlatformUserId, order.BuyerPlatformUserId),
            BuyerName = FirstNonEmpty(cachedRecord?.BuyerName, order.BuyerName),
            BuyerEmail = FirstNonEmpty(cachedRecord?.BuyerEmail, order.BuyerEmail),
            RecipientName = FirstNonEmpty(cachedRecord?.RecipientName, order.RecipientName),
            RecipientPhone = FirstNonEmpty(cachedRecord?.RecipientPhone, order.RecipientPhone),
            RecipientAddress = FirstNonEmpty(cachedRecord?.RecipientAddress, order.RecipientAddress),
            Status = FirstNonEmpty(cachedRecord?.Status, order.Status),
            TotalAmount = cachedRecord?.TotalAmount ?? order.TotalAmount,
            Currency = FirstNonEmpty(cachedRecord?.Currency, order.Currency),
            CreatedAtUtc = cachedRecord?.CreatedAtUtc ?? order.CreatedAtUtc,
            UpdatedAtUtc = cachedRecord?.UpdatedAtUtc ?? order.UpdatedAtUtc,
            PaidAtUtc = cachedRecord?.PaidAtUtc ?? order.PaidAtUtc,
            ProcessedAtUtc = cachedRecord?.ProcessedAtUtc,
            PrintedAtUtc = cachedRecord?.PrintedAtUtc,
            PrintCount = cachedRecord?.PrintCount ?? 0,
            PrintError = cachedRecord?.PrintError ?? string.Empty,
            ItemCount = groupedItems.Count,
            TotalQuantity = OrderItemGrouping.GetTotalQuantity(order.Items),
            PrimaryItemSummary = OrderItemGrouping.BuildCompactSummary(order.Items)
        };
    }

    public void ApplyRecordOverlay(OrderPrintModel order, PrintedOrderRecord? record)
    {
        if (record is null)
        {
            return;
        }

        if (string.Equals(record.BuyerAccountNameSource, "seller_center_bridge", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(record.BuyerAccountName))
        {
            order.BuyerAccountName = record.BuyerAccountName;
        }
        else if (string.IsNullOrWhiteSpace(order.BuyerAccountName))
        {
            order.BuyerAccountName = record.BuyerAccountName;
        }

        if (string.IsNullOrWhiteSpace(order.BuyerPlatformUserId))
        {
            order.BuyerPlatformUserId = record.BuyerPlatformUserId;
        }

        if (string.IsNullOrWhiteSpace(order.BuyerName))
        {
            order.BuyerName = record.BuyerName;
        }

        if (string.IsNullOrWhiteSpace(order.BuyerEmail))
        {
            order.BuyerEmail = record.BuyerEmail;
        }

        if (string.IsNullOrWhiteSpace(order.RecipientName))
        {
            order.RecipientName = record.RecipientName;
        }

        if (string.IsNullOrWhiteSpace(order.RecipientPhone))
        {
            order.RecipientPhone = record.RecipientPhone;
        }

        if (string.IsNullOrWhiteSpace(order.RecipientAddress))
        {
            order.RecipientAddress = record.RecipientAddress;
        }
    }

    private static void CollectOrderIds(JsonNode? node, ISet<string> results)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Key.Equals("order_id", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = property.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            results.Add(value);
                        }
                    }
                    else if (property.Key.Equals("order_id_list", StringComparison.OrdinalIgnoreCase) &&
                             property.Value is JsonArray idArray)
                    {
                        foreach (var idNode in idArray)
                        {
                            var idValue = idNode?.ToString();
                            if (!string.IsNullOrWhiteSpace(idValue))
                            {
                                results.Add(idValue);
                            }
                        }
                    }

                    CollectOrderIds(property.Value, results);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectOrderIds(item, results);
                }

                break;
        }
    }

    private static JsonObject? FindFirstOrderObject(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj when IsOrderLikeObject(obj):
                return obj;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    var match = FindFirstOrderObject(property.Value);
                    if (match is not null)
                    {
                        return match;
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    var match = FindFirstOrderObject(item);
                    if (match is not null)
                    {
                        return match;
                    }
                }

                break;
        }

        return null;
    }

    private static bool IsOrderLikeObject(JsonObject obj)
    {
        if (obj.ContainsKey("order_id"))
        {
            return true;
        }

        return obj.ContainsKey("id") &&
               (obj.ContainsKey("status") ||
                obj.ContainsKey("order_status") ||
                obj.ContainsKey("line_items") ||
                obj.ContainsKey("payment") ||
                obj.ContainsKey("payment_info") ||
                obj.ContainsKey("recipient_address"));
    }

    private static List<OrderItemPrintModel> ExtractItems(JsonObject orderObject)
    {
        foreach (var path in new[] { "line_items", "sku_list", "item_list", "order_line_list", "product_list" })
        {
            if (TryGetByPath(orderObject, path) is JsonArray itemsArray)
            {
                return itemsArray
                    .OfType<JsonObject>()
                    .Select(MapItem)
                    .ToList();
            }
        }

        var fallback = FindFirstArrayWithKey(orderObject, "quantity", "product_name", "sku_name", "item_name");
        return fallback is null
            ? []
            : fallback.OfType<JsonObject>().Select(MapItem).ToList();
    }

    private static OrderItemPrintModel MapItem(JsonObject item)
    {
        var title = ExtractString(item, "product_name", "item_name", "title", "name", "product_title", "sku_name", "seller_sku");
        var variant = ExtractPrimaryVariant(item);
        var sku = ExtractPrimarySku(item);

        if (string.IsNullOrWhiteSpace(variant) && LooksLikeVariantLabel(sku))
        {
            variant = sku;
        }

        return new OrderItemPrintModel
        {
            Title = title,
            Variant = variant,
            Sku = sku,
            Quantity = ExtractDecimal(item, "quantity", "count", "qty", "sku_count"),
            UnitPrice = ExtractDecimal(item, "sale_price", "price", "unit_price", "original_price")
        };
    }

    private static string BuildAddress(JsonObject orderObject)
    {
        var addressNode = TryGetByPath(orderObject, "recipient_address") as JsonObject
                          ?? TryGetByPath(orderObject, "shipping_address") as JsonObject
                          ?? TryGetByPath(orderObject, "delivery_address") as JsonObject;

        if (addressNode is null)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        AddIfValue(lines, ExtractString(addressNode, "full_address"));
        AddIfValue(lines, JoinParts(", ",
            ExtractString(addressNode, "address_line1", "address1", "street"),
            ExtractString(addressNode, "address_line2", "address2"),
            ExtractString(addressNode, "address_line3"),
            ExtractString(addressNode, "address_line4"),
            ExtractString(addressNode, "address_detail")));

        AddIfValue(lines, ExtractDistrictLine(addressNode));

        AddIfValue(lines, JoinParts(", ",
            ExtractString(addressNode, "district", "city", "post_town"),
            ExtractString(addressNode, "state", "province"),
            ExtractString(addressNode, "postal_code", "zip_code", "zipcode")));

        AddIfValue(lines, JoinParts(" ",
            ExtractString(addressNode, "country"),
            ExtractString(addressNode, "country_code", "region_code")));

        var dropOff = ExtractString(addressNode, "delivery_preferences.drop_off_location");
        if (!string.IsNullOrWhiteSpace(dropOff))
        {
            AddIfValue(lines, $"Drop-off: {dropOff}");
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string ExtractDistrictLine(JsonObject addressNode)
    {
        if (addressNode["district_info"] is not JsonArray districtInfo)
        {
            return string.Empty;
        }

        var names = districtInfo
            .OfType<JsonObject>()
            .Select(item => ExtractString(item, "address_name"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count == 0 ? string.Empty : string.Join(", ", names);
    }

    private static string ExtractPrimaryVariant(JsonObject item)
    {
        var directVariant = ExtractStringStrict(item,
            "sku_name",
            "variation_name",
            "variant_name",
            "model_name",
            "specification_name",
            "option_name");
        if (!string.IsNullOrWhiteSpace(directVariant))
        {
            return NormalizeInlineValue(directVariant);
        }

        var nestedVariant = ExtractFirstStringFromArray(item, "sub_item_info",
            "sku_name",
            "variation_name",
            "variant_name",
            "model_name",
            "specification_name",
            "option_name");
        if (!string.IsNullOrWhiteSpace(nestedVariant))
        {
            return NormalizeInlineValue(nestedVariant);
        }

        var combinedVariant = ExtractFirstStringFromArray(item, "combined_listing_skus",
            "sku_name",
            "seller_sku");
        if (!string.IsNullOrWhiteSpace(combinedVariant))
        {
            return NormalizeInlineValue(combinedVariant);
        }

        var skuLikeVariant = FirstNonEmpty(
            ExtractStringStrict(item, "seller_sku"),
            FirstNonEmpty(
                ExtractFirstStringFromArray(item, "sub_item_info", "seller_sku"),
                ExtractFirstStringFromArray(item, "combined_listing_skus", "seller_sku")));

        return LooksLikeVariantLabel(skuLikeVariant) ? NormalizeInlineValue(skuLikeVariant) : string.Empty;
    }

    private static string ExtractPrimarySku(JsonObject item)
    {
        var directSku = ExtractStringStrict(item, "seller_sku", "sku_id", "sku", "seller_sku_id");
        if (!string.IsNullOrWhiteSpace(directSku))
        {
            return NormalizeInlineValue(directSku);
        }

        var nestedSku = ExtractFirstStringFromArray(item, "sub_item_info", "seller_sku", "sku_id", "sku", "seller_sku_id");
        if (!string.IsNullOrWhiteSpace(nestedSku))
        {
            return NormalizeInlineValue(nestedSku);
        }

        var combinedSku = ExtractFirstStringFromArray(item, "combined_listing_skus", "seller_sku", "sku_id", "sku", "seller_sku_id");
        return NormalizeInlineValue(combinedSku);
    }

    private static string ExtractString(JsonObject node, params string[] candidatePaths)
    {
        var directValue = ExtractStringStrict(node, candidatePaths);
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue;
        }

        var keys = candidatePaths.Select(x => x.Contains('.') ? x.Split('.').Last() : x).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var recursiveValue = FindFirstValueByKeys(node, keys);
        return recursiveValue?.ToString() ?? string.Empty;
    }

    private static string ExtractStringStrict(JsonObject node, params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (TryGetByPath(node, path) is { } directValue && !string.IsNullOrWhiteSpace(directValue.ToString()))
            {
                return directValue.ToString();
            }
        }

        return string.Empty;
    }

    private static string ExtractFirstStringFromArray(JsonObject node, string arrayPath, params string[] candidatePaths)
    {
        if (TryGetByPath(node, arrayPath) is not JsonArray array)
        {
            return string.Empty;
        }

        foreach (var child in array.OfType<JsonObject>())
        {
            var strictValue = ExtractStringStrict(child, candidatePaths);
            if (!string.IsNullOrWhiteSpace(strictValue))
            {
                return strictValue;
            }

            var recursiveValue = ExtractString(child, candidatePaths);
            if (!string.IsNullOrWhiteSpace(recursiveValue))
            {
                return recursiveValue;
            }
        }

        return string.Empty;
    }

    private static decimal? ExtractDecimal(JsonObject node, params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (TryGetByPath(node, path) is { } directValue && TryParseDecimal(directValue, out var parsed))
            {
                return parsed;
            }
        }

        var keys = candidatePaths.Select(x => x.Contains('.') ? x.Split('.').Last() : x).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var recursiveValue = FindFirstValueByKeys(node, keys);
        return recursiveValue is not null && TryParseDecimal(recursiveValue, out var value) ? value : null;
    }

    private static DateTimeOffset? ExtractDateTime(JsonObject node, params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (TryGetByPath(node, path) is { } directValue && TryParseDateTime(directValue, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static JsonArray? FindFirstArrayWithKey(JsonNode? node, params string[] keys)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray candidateArray &&
                        candidateArray.OfType<JsonObject>().Any(item => keys.Any(key => item.ContainsKey(key))))
                    {
                        return candidateArray;
                    }

                    var nested = FindFirstArrayWithKey(property.Value, keys);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    var nested = FindFirstArrayWithKey(item, keys);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                break;
        }

        return null;
    }

    private static JsonNode? FindFirstValueByKeys(JsonNode? node, params string[] keys)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in keys)
                {
                    if (obj.TryGetPropertyValue(key, out var value) && value is not null)
                    {
                        return value;
                    }
                }

                foreach (var property in obj)
                {
                    var nested = FindFirstValueByKeys(property.Value, keys);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    var nested = FindFirstValueByKeys(item, keys);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                break;
        }

        return null;
    }

    private static JsonNode? TryGetByPath(JsonNode? node, string path)
    {
        var current = node;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(segment, out var next))
            {
                current = next;
                continue;
            }

            return null;
        }

        return current;
    }

    private static bool TryParseDecimal(JsonNode node, out decimal value)
    {
        if (node is JsonObject obj)
        {
            var nested = FindFirstValueByKeys(obj, "amount", "value", "price");
            if (nested is not null)
            {
                return TryParseDecimal(nested, out value);
            }
        }

        return decimal.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseDateTime(JsonNode node, out DateTimeOffset value)
    {
        if (long.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            value = DateTimeOffset.FromUnixTimeSeconds(unix);
            return true;
        }

        return DateTimeOffset.TryParse(node.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }

    private static void AddIfValue(ICollection<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value.Trim());
        }
    }

    private static string JoinParts(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));

    private static string JoinName(string firstName, string lastName) =>
        JoinParts(" ", firstName, lastName);

    private static string NormalizeInlineValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                    lastWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            lastWasWhitespace = false;
        }

        return builder.ToString();
    }

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

    private static string BuildPrimaryItemSummary(IReadOnlyList<OrderItemPrintModel> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var firstItem = items[0];
        var title = string.IsNullOrWhiteSpace(firstItem.Title) ? "未命名商品" : firstItem.Title.Trim();
        if (items.Count == 1)
        {
            return title;
        }

        return $"{title} 等 {items.Count} 件商品";
    }

    private static string FirstNonEmpty(string? first, string second) =>
        string.IsNullOrWhiteSpace(first) ? second : first;
}




