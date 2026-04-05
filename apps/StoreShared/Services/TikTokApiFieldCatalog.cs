using System.Text;
using System.Text.Json.Nodes;
using TikTokOrderPrinter.Models;

namespace TikTokOrderPrinter.Services;

public static class TikTokApiFieldCatalog
{
    private static readonly Dictionary<string, string> ExactLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["$"] = "根节点",
        ["id"] = "订单 ID",
        ["user_id"] = "买家 User ID",
        ["buyer_nickname"] = "买家昵称 ID",
        ["buyer_avatar"] = "买家头像",
        ["buyer_email"] = "买家邮箱",
        ["buyer_message"] = "买家留言",
        ["status"] = "订单状态",
        ["create_time"] = "下单时间",
        ["paid_time"] = "支付时间",
        ["update_time"] = "更新时间",
        ["rts_time"] = "备货时间",
        ["rts_sla_time"] = "备货 SLA 时间",
        ["tts_sla_time"] = "TikTok SLA 时间",
        ["shipping_due_time"] = "最晚发货时间",
        ["collection_due_time"] = "最晚揽收时间",
        ["delivery_due_time"] = "最晚送达时间",
        ["delivery_time"] = "配送时间",
        ["delivery_option_name"] = "配送选项名称",
        ["delivery_option_id"] = "配送选项 ID",
        ["delivery_type"] = "配送类型",
        ["shipping_type"] = "物流类型",
        ["shipping_provider"] = "物流商",
        ["shipping_provider_id"] = "物流商 ID",
        ["payment.currency"] = "付款 / 币种",
        ["payment.total_amount"] = "付款 / 订单总额",
        ["payment.sub_total"] = "付款 / 商品小计",
        ["payment.tax"] = "付款 / 税费",
        ["payment.original_shipping_fee"] = "付款 / 原始运费",
        ["payment.original_total_product_price"] = "付款 / 原始商品总价",
        ["payment.platform_discount"] = "付款 / 平台优惠",
        ["payment.seller_discount"] = "付款 / 商家优惠",
        ["recipient_address.full_address"] = "收件地址 / 完整地址",
        ["recipient_address.phone_number"] = "收件地址 / 电话",
        ["recipient_address.name"] = "收件地址 / 收件人",
        ["recipient_address.first_name"] = "收件地址 / 名",
        ["recipient_address.last_name"] = "收件地址 / 姓",
        ["recipient_address.region_code"] = "收件地址 / 国家地区代码",
        ["recipient_address.postal_code"] = "收件地址 / 邮编",
        ["recipient_address.address_detail"] = "收件地址 / 详细地址",
        ["line_items"] = "商品项",
        ["packages"] = "包裹列表",
        ["order_rights"] = "订单权益"
    };

    private static readonly Dictionary<string, string> SegmentLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "ID",
        ["cancellation_initiator"] = "取消发起方",
        ["shipping_provider"] = "物流商",
        ["shipping_provider_id"] = "物流商 ID",
        ["user_id"] = "买家 User ID",
        ["status"] = "状态",
        ["rts_time"] = "备货时间",
        ["payment"] = "付款",
        ["currency"] = "币种",
        ["sub_total"] = "商品小计",
        ["shipping_fee"] = "运费",
        ["seller_discount"] = "商家优惠",
        ["platform_discount"] = "平台优惠",
        ["payment_platform_discount"] = "支付平台优惠",
        ["payment_discount_service_fee"] = "支付优惠服务费",
        ["total_amount"] = "订单总额",
        ["original_total_product_price"] = "原始商品总价",
        ["original_shipping_fee"] = "原始运费",
        ["shipping_fee_seller_discount"] = "运费商家优惠",
        ["shipping_fee_platform_discount"] = "运费平台优惠",
        ["shipping_fee_cofunded_discount"] = "运费共担优惠",
        ["tax"] = "税费",
        ["small_order_fee"] = "小单费",
        ["shipping_fee_tax"] = "运费税费",
        ["product_tax"] = "商品税费",
        ["retail_delivery_fee"] = "零售配送费",
        ["buyer_service_fee"] = "买家服务费",
        ["handling_fee"] = "处理费",
        ["shipping_insurance_fee"] = "物流保险费",
        ["item_insurance_fee"] = "商品保险费",
        ["item_insurance_tax"] = "商品保险税费",
        ["distance_shipping_fee"] = "距离运费",
        ["distance_fee"] = "距离费用",
        ["recipient_address"] = "收件地址",
        ["full_address"] = "完整地址",
        ["phone_number"] = "电话",
        ["name"] = "名称",
        ["first_name"] = "名",
        ["last_name"] = "姓",
        ["first_name_local_script"] = "本地名",
        ["last_name_local_script"] = "本地姓",
        ["region_code"] = "国家地区代码",
        ["postal_code"] = "邮编",
        ["post_town"] = "城镇",
        ["address_line1"] = "地址行 1",
        ["address_line2"] = "地址行 2",
        ["address_line3"] = "地址行 3",
        ["address_line4"] = "地址行 4",
        ["district_info"] = "地址层级",
        ["delivery_preferences"] = "配送偏好",
        ["drop_off_location"] = "投递位置",
        ["address_detail"] = "详细地址",
        ["buyer_message"] = "买家留言",
        ["create_time"] = "下单时间",
        ["tracking_number"] = "运单号",
        ["cancel_reason"] = "取消原因",
        ["rts_sla_time"] = "备货 SLA 时间",
        ["fulfillment_type"] = "履约方式",
        ["seller_note"] = "卖家备注",
        ["tts_sla_time"] = "TikTok SLA 时间",
        ["cancel_order_sla_time"] = "取消 SLA 时间",
        ["update_time"] = "更新时间",
        ["packages"] = "包裹",
        ["delivery_type"] = "配送类型",
        ["is_sample_order"] = "是否样品单",
        ["warehouse_id"] = "仓库 ID",
        ["split_or_combine_tag"] = "拆单合单标记",
        ["has_updated_recipient_address"] = "是否修改收件地址",
        ["cpf"] = "税号",
        ["delivery_option_id"] = "配送选项 ID",
        ["delivery_sla_time"] = "配送 SLA 时间",
        ["payment_method_name"] = "支付方式名称",
        ["payment_card_type"] = "支付卡类型",
        ["channel_entity_national_registry_id"] = "主体注册号",
        ["payment_method_code"] = "支付方式代码",
        ["payment_auth_code"] = "支付授权码",
        ["shipping_due_time"] = "最晚发货时间",
        ["line_items"] = "商品项",
        ["sku_id"] = "SKU ID",
        ["combined_listing_skus"] = "组合商品 SKU",
        ["sku_count"] = "SKU 数量",
        ["product_id"] = "商品 ID",
        ["seller_sku"] = "商家 SKU",
        ["product_name"] = "商品名称",
        ["sku_name"] = "规格名称",
        ["sku_image"] = "规格图片",
        ["original_price"] = "原价",
        ["sale_price"] = "售价",
        ["pfand_fee"] = "押金费",
        ["display_status"] = "展示状态",
        ["cancel_user"] = "取消方",
        ["sku_type"] = "SKU 类型",
        ["package_id"] = "包裹 ID",
        ["item_tax"] = "商品税项",
        ["package_status"] = "包裹状态",
        ["shipping_provider_name"] = "物流商名称",
        ["is_gift"] = "是否赠品",
        ["handling_duration_days"] = "处理天数",
        ["is_dangerous_good"] = "是否危险品",
        ["needs_prescription"] = "是否需处方",
        ["gift_retail_price"] = "赠品零售价",
        ["is_unboxing_item"] = "是否开箱商品",
        ["unboxing_sku_code"] = "开箱 SKU 码",
        ["sub_item_info"] = "子商品信息",
        ["tax_type"] = "税种",
        ["tax_amount"] = "税额",
        ["tax_rate"] = "税率",
        ["buyer_email"] = "买家邮箱",
        ["delivery_time"] = "配送时间",
        ["need_upload_invoice"] = "是否需上传发票",
        ["is_cod"] = "是否货到付款",
        ["request_cancel_time"] = "申请取消时间",
        ["delivery_option_required_delivery_time"] = "要求送达时间",
        ["delivery_option_name"] = "配送选项名称",
        ["is_buyer_request_cancel"] = "是否买家申请取消",
        ["delivery_due_time"] = "最晚送达时间",
        ["collection_time"] = "揽收时间",
        ["is_on_hold_order"] = "是否挂起订单",
        ["cancel_time"] = "取消时间",
        ["is_replacement_order"] = "是否补发订单",
        ["replaced_order_id"] = "被替换订单 ID",
        ["collection_due_time"] = "最晚揽收时间",
        ["pick_up_cut_off_time"] = "揽收截单时间",
        ["fast_dispatch_sla_time"] = "极速发货 SLA 时间",
        ["commerce_platform"] = "电商平台",
        ["order_type"] = "订单类型",
        ["release_date"] = "发布日期",
        ["handling_duration"] = "处理时长",
        ["days"] = "天数",
        ["type"] = "类型",
        ["auto_combine_group_id"] = "自动合单组 ID",
        ["cpf_name"] = "税号姓名",
        ["is_exchange_order"] = "是否换货单",
        ["exchange_source_order_id"] = "换货来源订单 ID",
        ["consultation_id"] = "咨询 ID",
        ["fast_delivery_program"] = "快速配送计划",
        ["buyer_nickname"] = "买家昵称 ID",
        ["buyer_avatar"] = "买家头像",
        ["order_rights"] = "订单权益",
        ["fulfillment_priority_level"] = "履约优先级",
        ["recommended_shipping_time"] = "建议发货时间",
        ["address_level_name"] = "地址层级名称",
        ["address_name"] = "地址名称",
        ["address_level"] = "地址层级",
        ["iso_code"] = "ISO 代码"
    };

    public static List<ApiFieldOption> Flatten(JsonNode? node)
    {
        var results = new List<ApiFieldOption>();
        FlattenCore(node, string.Empty, results);
        return results
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ToChineseLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "未命名字段";
        }

        if (ExactLabels.TryGetValue(path, out var exact))
        {
            return exact;
        }

        var localizedSegments = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(LocalizeSegment)
            .ToArray();

        return localizedSegments.Length == 0 ? path : string.Join(" / ", localizedSegments);
    }

    private static void FlattenCore(JsonNode? node, string prefix, ICollection<ApiFieldOption> results)
    {
        switch (node)
        {
            case null:
                results.Add(CreateField(prefix, null));
                return;
            case JsonObject obj:
                if (obj.Count == 0)
                {
                    results.Add(CreateField(prefix, obj));
                    return;
                }

                foreach (var property in obj)
                {
                    var nextPrefix = string.IsNullOrWhiteSpace(prefix) ? property.Key : $"{prefix}.{property.Key}";
                    FlattenCore(property.Value, nextPrefix, results);
                }

                return;
            case JsonArray array:
                if (array.Count == 0)
                {
                    results.Add(CreateField(prefix, array));
                    return;
                }

                for (var index = 0; index < array.Count; index++)
                {
                    var nextPrefix = string.IsNullOrWhiteSpace(prefix) ? $"[{index}]" : $"{prefix}[{index}]";
                    FlattenCore(array[index], nextPrefix, results);
                }

                return;
            default:
                results.Add(CreateField(prefix, node));
                return;
        }
    }

    private static ApiFieldOption CreateField(string path, JsonNode? node) =>
        new()
        {
            Path = string.IsNullOrWhiteSpace(path) ? "$" : path,
            Label = ToChineseLabel(string.IsNullOrWhiteSpace(path) ? "$" : path),
            ValueJson = node?.ToJsonString() ?? "null",
            DisplayValue = ToDisplayValue(node)
        };

    private static string ToDisplayValue(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        return node switch
        {
            JsonObject => node.ToJsonString(),
            JsonArray => node.ToJsonString(),
            _ => node.ToString()
        };
    }

    private static string LocalizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return segment;
        }

        var bracketIndex = segment.IndexOf('[');
        var key = bracketIndex >= 0 ? segment[..bracketIndex] : segment;
        var suffix = bracketIndex >= 0 ? segment[bracketIndex..] : string.Empty;

        if (!SegmentLabels.TryGetValue(key, out var label))
        {
            label = HumanizeKey(key);
        }

        if (string.IsNullOrWhiteSpace(suffix))
        {
            return label;
        }

        return label + NormalizeArraySuffix(suffix);
    }

    private static string HumanizeKey(string key)
    {
        var words = key
            .Replace("-", "_", StringComparison.Ordinal)
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => string.IsNullOrWhiteSpace(word)
                ? word
                : $"{char.ToUpperInvariant(word[0])}{word[1..]}")
            .ToArray();

        return words.Length == 0 ? key : string.Join(' ', words);
    }

    private static string NormalizeArraySuffix(string suffix)
    {
        var builder = new StringBuilder();
        var index = 0;
        while (index < suffix.Length)
        {
            if (suffix[index] == '[')
            {
                var end = suffix.IndexOf(']', index + 1);
                if (end > index)
                {
                    var raw = suffix.Substring(index + 1, end - index - 1);
                    if (int.TryParse(raw, out var parsed))
                    {
                        builder.Append('[').Append(parsed + 1).Append(']');
                    }
                    else
                    {
                        builder.Append('[').Append(raw).Append(']');
                    }

                    index = end + 1;
                    continue;
                }
            }

            builder.Append(suffix[index]);
            index++;
        }

        return builder.ToString();
    }
}
