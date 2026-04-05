using ClosedXML.Excel;
using TikTokSalesStats.Models;

namespace TikTokSalesStats.Services;

public sealed class SalesWorkbookExporter
{
    public byte[] BuildWorkbook(SalesSummaryResponse summary)
    {
        using var workbook = new XLWorkbook();

        AddOverviewSheet(workbook, summary);
        AddReconciliationSheet(workbook, summary);
        AddInsightsSheet(workbook, summary);
        AddOrdersSheet(workbook, "计入销售订单", summary.IncludedOrders, true);
        AddOrdersSheet(workbook, "排除订单", summary.ExcludedOrders, false);
        AddDailySheet(workbook, summary);
        AddMonthlySheet(workbook, summary);
        AddPaymentSheet(workbook, summary);
        AddBuyerRankingSheet(workbook, summary);
        AddUnpaidReminderSheet(workbook, summary);
        AddCustomerRiskSheet(workbook, "潜在客户", summary.PotentialCustomers);
        AddCustomerRiskSheet(workbook, "黑名单候选", summary.BlacklistCandidates);
        AddTopProductsSheet(workbook, summary);
        AddProductIdSheet(workbook, summary);
        AddSelectedProductSheet(workbook, summary);
        AddLinkAttributionSheet(workbook, summary);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddOverviewSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("总览");
        var overview = summary.Overview;
        var rows = new (string Label, object Value)[]
        {
            ("店铺", summary.StoreName),
            ("统计区间", $"{summary.FromUtc:yyyy-MM-dd} ~ {summary.ToUtc:yyyy-MM-dd}"),
            ("生成时间", summary.GeneratedAtUtc.LocalDateTime),
            ("观察订单数", overview.ObservedOrderCount),
            ("计入销售订单数", overview.IncludedOrderCount),
            ("实付金额", overview.IncludedPaidAmount),
            ("TikTok 折扣", overview.IncludedTikTokDiscountAmount),
            ("成交额", overview.IncludedGrossWithDiscount),
            ("客单价", overview.AverageOrderValue),
            ("有效订单率", overview.ValidOrderRate / 100m),
            ("用户名覆盖率", overview.HandleCoverageRate / 100m),
            ("复购率", overview.RepeatBuyerRate / 100m),
            ("识别买家数", overview.UniqueBuyerCount),
            ("复购买家数", overview.RepeatBuyerCount),
            ("待揽收状态单", overview.AwaitingCollectionStatusCount),
            ("未支付排除", overview.ExcludedUnpaidCount),
            ("取消排除", overview.ExcludedCancelledCount),
            ("退款/关闭排除", overview.ExcludedRefundedCount)
        };

        WriteKeyValueSheet(sheet, "TikTok 电商数据看板", rows);
        sheet.Range("B10:B12").Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddReconciliationSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("对账");
        var reconciliation = summary.Reconciliation;
        var settings = summary.Settings;

        var rows = new (string Label, object Value)[]
        {
            ("对账说明", reconciliation.Note),
            ("计入实付", reconciliation.BasePaidAmount),
            ("计入 TikTok 折扣", reconciliation.TikTokDiscountAmount),
            ("计入买家运费", reconciliation.BuyerShippingFeeAmount),
            ("预估平台费", reconciliation.EstimatedPlatformFeeAmount),
            ("预估物流成本", reconciliation.EstimatedLogisticsCostAmount),
            ("预估可回款金额", reconciliation.EstimatedReceivableAmount),
            ("预估已回款金额", reconciliation.EstimatedSettledReceivableAmount),
            ("预估待回款金额", reconciliation.EstimatedPendingReceivableAmount),
            ("可对账订单数", reconciliation.ReconcilableOrderCount),
            ("已完成结算订单数", reconciliation.CompletedOrderCount),
            ("待结算订单数", reconciliation.PendingSettlementOrderCount),
            ("结算完成率", reconciliation.SettlementCompletionRate / 100m),
            ("计入 TikTok 折扣", settings.IncludeTikTokDiscount ? "是" : "否"),
            ("计入买家运费", settings.IncludeBuyerShippingFee ? "是" : "否"),
            ("扣除平台费", settings.DeductPlatformFee ? "是" : "否"),
            ("平台费率(%)", settings.PlatformFeeRate),
            ("扣除物流费", settings.DeductLogisticsCost ? "是" : "否"),
            ("单票物流成本", settings.LogisticsCostPerOrder)
        };

        WriteKeyValueSheet(sheet, "对账与回款估算", rows);
        sheet.Cell("B13").Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddInsightsSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("经营洞察");
        var insights = new[]
        {
            summary.Insights.BestDay,
            summary.Insights.BestHour,
            summary.Insights.BestStore,
            summary.Insights.BestPayment,
            summary.Insights.BestBuyer,
            summary.Insights.BestProduct
        };

        WriteTable(
            sheet,
            ["指标", "标签", "数值", "说明"],
            insights.Select(item => new object[] { item.Title, item.Label, item.Value, item.Note }).ToList());
    }

    private static void AddOrdersSheet(XLWorkbook workbook, string sheetName, IReadOnlyList<OrderLineSummary> orders, bool included)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteTable(
            sheet,
            [
                "店铺", "订单号", "用户名", "Buyer User ID", "邮箱别名", "订单状态", "回款状态", "支付方式", "支付时间", "下单时间",
                "商品摘要", "链接归因", "链接 URL", "商品行数", "实付金额", "TikTok 折扣", "买家运费", "预估平台费", "预估物流费", "预估可回款",
                included ? "统计口径" : "排除原因"
            ],
            orders.Select(order => new object[]
            {
                order.StoreName,
                order.OrderId,
                order.BuyerHandle,
                order.BuyerUserId,
                order.BuyerEmail,
                order.Status,
                order.SettlementState,
                order.PaymentMethod,
                order.PaidAtLocal,
                order.CreatedAtLocal,
                order.PrimaryProductName,
                order.LinkAttributionLabel,
                order.LinkAttributionUrl,
                order.ItemLineCount,
                order.PaidAmount,
                order.TikTokDiscountAmount,
                order.BuyerShippingFeeAmount,
                order.EstimatedPlatformFeeAmount,
                order.EstimatedLogisticsCostAmount,
                order.EstimatedReceivableAmount,
                included ? "计入销售" : order.ExclusionReason
            }).ToList());
    }

    private static void AddDailySheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("按日汇总");
        WriteTable(
            sheet,
            ["日期", "订单数", "实付金额", "TikTok 折扣", "成交额"],
            summary.Daily.Select(item => new object[] { item.Date, item.OrderCount, item.PaidAmount, item.TikTokDiscountAmount, item.GrossWithDiscount }).ToList());
    }

    private static void AddMonthlySheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("按月汇总");
        WriteTable(
            sheet,
            ["月份", "订单数", "实付金额", "TikTok 折扣", "成交额"],
            summary.Monthly.Select(item => new object[] { item.Month, item.OrderCount, item.PaidAmount, item.TikTokDiscountAmount, item.GrossWithDiscount }).ToList());
    }

    private static void AddPaymentSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("支付方式");
        WriteTable(
            sheet,
            ["支付方式", "订单数", "实付金额", "TikTok 折扣", "成交额", "实付占比"],
            summary.PaymentBreakdown.Select(item => new object[]
            {
                item.PaymentMethod,
                item.OrderCount,
                item.PaidAmount,
                item.TikTokDiscountAmount,
                item.GrossWithDiscount,
                item.PaidAmountShareRate / 100m
            }).ToList());
        sheet.Column(6).Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddBuyerRankingSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("支付排行榜");
        WriteTable(
            sheet,
            ["排名", "买家", "订单数", "实付金额", "TikTok 折扣", "成交额", "涉及店铺数"],
            summary.PaidBuyerRanking.Select((item, index) => new object[]
            {
                index + 1,
                item.BuyerLabel,
                item.OrderCount,
                item.PaidAmount,
                item.TikTokDiscountAmount,
                item.GrossWithDiscount,
                item.StoreCount
            }).ToList());
    }

    private static void AddUnpaidReminderSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("未支付提醒");
        WriteTable(
            sheet,
            ["店铺", "订单号", "买家", "Buyer User ID", "邮箱别名", "支付方式", "应付金额", "下单时间", "未支付小时数", "便利店支付", "是否首单"],
            summary.UnpaidReminders.Select(item => new object[]
            {
                item.StoreName,
                item.OrderId,
                item.BuyerLabel,
                item.BuyerUserId,
                item.BuyerEmail,
                item.PaymentMethod,
                item.ExpectedAmount,
                item.CreatedAtLocal,
                item.HoursOpen,
                item.IsConvenienceStorePayment ? "是" : "否",
                item.IsFirstObservedOrder ? "是" : "否"
            }).ToList());
    }

    private static void AddCustomerRiskSheet(XLWorkbook workbook, string sheetName, IReadOnlyList<CustomerRiskSummary> rows)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteTable(
            sheet,
            ["店铺", "买家", "Buyer User ID", "邮箱别名", "原因", "首单号", "首单时间", "触发订单号", "触发状态", "触发时间", "触发金额"],
            rows.Select(item => new object[]
            {
                item.StoreName,
                item.BuyerLabel,
                item.BuyerUserId,
                item.BuyerEmail,
                item.Reason,
                item.FirstOrderId,
                item.FirstOrderAtLocal,
                item.TriggerOrderId,
                item.TriggerStatus,
                item.TriggerAtLocal,
                item.TriggerAmount
            }).ToList());
    }

    private static void AddTopProductsSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("热销商品");
        WriteTable(
            sheet,
            ["Product ID", "商品", "订单数", "商品行数", "件数", "销售额", "TikTok 折扣", "成交额"],
            summary.TopProducts.Select(item => new object[]
            {
                item.ProductId,
                item.DisplayName,
                item.OrderCount,
                item.ItemLineCount,
                item.Quantity,
                item.SalesAmount,
                item.TikTokDiscountAmount,
                item.GrossWithDiscount
            }).ToList());
    }

    private static void AddProductIdSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("Product ID汇总");
        WriteTable(
            sheet,
            ["Product ID", "商品", "订单数", "SKU数", "商品行数", "件数", "涉及店铺数", "销售额", "TikTok 折扣", "成交额"],
            summary.ProductIdBreakdown.Select(item => new object[]
            {
                item.ProductId,
                item.ProductName,
                item.OrderCount,
                item.SkuCount,
                item.ItemLineCount,
                item.Quantity,
                item.StoreCount,
                item.SalesAmount,
                item.TikTokDiscountAmount,
                item.GrossWithDiscount
            }).ToList());
    }

    private static void AddSelectedProductSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var selectedProductIds = summary.SelectedProductIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedProducts = selectedProductIds.Count == 0
            ? []
            : summary.ProductIdBreakdown.Where(item => selectedProductIds.Contains(item.ProductId)).ToList();

        var sheet = workbook.Worksheets.Add("Product ID明细");
        if (selectedProducts.Count == 0)
        {
            sheet.Cell(1, 1).Value = "未选择 Product ID";
            sheet.Cell(2, 1).Value = "在看板中输入 Product ID 后再导出，这里会给出选中产品的店铺拆分和 SKU 汇总。";
            sheet.Columns().AdjustToContents();
            return;
        }

        var row = 1;
        foreach (var product in selectedProducts)
        {
            sheet.Cell(row, 1).Value = $"{product.ProductName} ({product.ProductId})";
            sheet.Range(row, 1, row, 8).Merge().Style.Font.SetBold().Font.SetFontSize(14);
            row += 1;

            WriteTableAt(
                sheet,
                row,
                ["店铺", "订单数", "商品行数", "件数", "销售额", "TikTok 折扣", "成交额"],
                product.StoreBreakdown.Select(item => new object[]
                {
                    item.StoreName,
                    item.OrderCount,
                    item.ItemLineCount,
                    item.Quantity,
                    item.SalesAmount,
                    item.TikTokDiscountAmount,
                    item.GrossWithDiscount
                }).ToList());

            row += product.StoreBreakdown.Count + 3;

            WriteTableAt(
                sheet,
                row,
                ["SKU ID", "SKU", "订单数", "商品行数", "件数", "销售额", "TikTok 折扣", "成交额"],
                product.SkuBreakdown.Select(item => new object[]
                {
                    item.SkuId,
                    item.DisplayName,
                    item.OrderCount,
                    item.ItemLineCount,
                    item.Quantity,
                    item.SalesAmount,
                    item.TikTokDiscountAmount,
                    item.GrossWithDiscount
                }).ToList());

            row += product.SkuBreakdown.Count + 4;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void AddLinkAttributionSheet(XLWorkbook workbook, SalesSummaryResponse summary)
    {
        var sheet = workbook.Worksheets.Add("链接归因");
        WriteTable(
            sheet,
            ["链接标签", "链接 URL", "订单数", "商品行数", "买家数", "店铺数", "实付金额", "TikTok 折扣", "成交额"],
            summary.LinkAttributionBreakdown.Select(item => new object[]
            {
                item.Label,
                item.LinkUrl,
                item.OrderCount,
                item.ItemLineCount,
                item.UniqueBuyerCount,
                item.StoreCount,
                item.PaidAmount,
                item.TikTokDiscountAmount,
                item.GrossWithDiscount
            }).ToList());
    }

    private static void WriteKeyValueSheet(IXLWorksheet sheet, string title, IReadOnlyList<(string Label, object Value)> rows)
    {
        sheet.Cell("A1").Value = title;
        sheet.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16);

        for (var index = 0; index < rows.Count; index += 1)
        {
            var row = index + 3;
            sheet.Cell(row, 1).Value = rows[index].Label;
            sheet.Cell(row, 2).Value = XLCellValue.FromObject(rows[index].Value);
        }

        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 42;
        sheet.Column(1).Style.Font.SetBold();
        sheet.Columns().AdjustToContents();
    }

    private static void WriteTable(IXLWorksheet sheet, IReadOnlyList<string> headers, IReadOnlyList<object[]> rows)
    {
        WriteTableAt(sheet, 1, headers, rows);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents();
    }

    private static void WriteTableAt(IXLWorksheet sheet, int startRow, IReadOnlyList<string> headers, IReadOnlyList<object[]> rows)
    {
        for (var index = 0; index < headers.Count; index += 1)
        {
            sheet.Cell(startRow, index + 1).Value = headers[index];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex += 1)
        {
            for (var cellIndex = 0; cellIndex < rows[rowIndex].Length; cellIndex += 1)
            {
                sheet.Cell(startRow + rowIndex + 1, cellIndex + 1).Value = XLCellValue.FromObject(rows[rowIndex][cellIndex]);
            }
        }

        sheet.Row(startRow).Style.Font.SetBold();
    }
}
