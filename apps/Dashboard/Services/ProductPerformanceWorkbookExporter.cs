using ClosedXML.Excel;
using TikTokSalesStats.Models;

namespace TikTokSalesStats.Services;

public sealed class ProductPerformanceWorkbookExporter
{
    public byte[] BuildWorkbook(ProductPerformanceResponse summary)
    {
        using var workbook = new XLWorkbook();

        AddOverviewSheet(workbook, summary);
        AddProductSummarySheet(workbook, summary);
        AddStoreBreakdownSheet(workbook, summary);
        AddMonthlySheet(workbook, summary);
        AddDailySheet(workbook, summary);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddOverviewSheet(XLWorkbook workbook, ProductPerformanceResponse summary)
    {
        var sheet = workbook.Worksheets.Add("总览");
        var totals = summary.Totals;
        var rows = new (string Label, object Value)[]
        {
            ("店铺", summary.StoreName),
            ("统计区间", $"{summary.FromUtc:yyyy-MM-dd} ~ {summary.ToUtc:yyyy-MM-dd}"),
            ("生成时间", summary.GeneratedAtUtc.LocalDateTime),
            ("追踪产品数", totals.ProductCount),
            ("涉及订单数", totals.OrderCount),
            ("件数", totals.Quantity),
            ("实际支付", totals.PaidAmount),
            ("TikTok 折扣", totals.TikTokDiscountAmount),
            ("买家运费", totals.BuyerShippingFeeAmount),
            ("扣平台佣金", totals.EstimatedPlatformFeeAmount),
            ("扣物流", totals.EstimatedLogisticsCostAmount),
            ("预估可回款", totals.EstimatedReceivableAmount),
            ("已回款", totals.EstimatedSettledReceivableAmount),
            ("未回款", totals.EstimatedPendingReceivableAmount),
            ("回款完成率", totals.SettlementCompletionRate / 100m),
            ("已回款单数", totals.CompletedOrderCount),
            ("未回款单数", totals.PendingSettlementOrderCount)
        };

        WriteKeyValueSheet(sheet, "产品业绩窗口", rows);
        sheet.Cell("B15").Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddProductSummarySheet(XLWorkbook workbook, ProductPerformanceResponse summary)
    {
        var sheet = workbook.Worksheets.Add("Product业绩");
        WriteTable(
            sheet,
            [
                "Product ID", "标签", "商品", "订单数", "件数", "SKU数", "店铺数", "实际支付", "TikTok折扣", "买家运费", "扣平台佣金", "扣物流", "预估可回款", "已回款", "未回款", "成交额", "回款完成率"
            ],
            summary.Products.Select(item => new object[]
            {
                item.ProductId,
                item.Label,
                item.ProductName,
                item.OrderCount,
                item.Quantity,
                item.SkuCount,
                item.StoreCount,
                item.PaidAmount,
                item.TikTokDiscountAmount,
                item.BuyerShippingFeeAmount,
                item.EstimatedPlatformFeeAmount,
                item.EstimatedLogisticsCostAmount,
                item.EstimatedReceivableAmount,
                item.EstimatedSettledReceivableAmount,
                item.EstimatedPendingReceivableAmount,
                item.GrossWithDiscount,
                item.SettlementCompletionRate / 100m
            }).ToList());

        sheet.Column(17).Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddStoreBreakdownSheet(XLWorkbook workbook, ProductPerformanceResponse summary)
    {
        var sheet = workbook.Worksheets.Add("店铺拆分");
        WriteTable(
            sheet,
            [
                "Product ID", "标签", "店铺", "订单数", "件数", "实际支付", "TikTok折扣", "买家运费", "扣平台佣金", "扣物流", "预估可回款", "已回款", "未回款", "成交额", "回款完成率"
            ],
            summary.Products.SelectMany(product => product.StoreBreakdown.Select(store => new object[]
            {
                product.ProductId,
                product.Label,
                store.StoreName,
                store.OrderCount,
                store.Quantity,
                store.PaidAmount,
                store.TikTokDiscountAmount,
                store.BuyerShippingFeeAmount,
                store.EstimatedPlatformFeeAmount,
                store.EstimatedLogisticsCostAmount,
                store.EstimatedReceivableAmount,
                store.EstimatedSettledReceivableAmount,
                store.EstimatedPendingReceivableAmount,
                store.GrossWithDiscount,
                store.SettlementCompletionRate / 100m
            })).ToList());

        sheet.Column(15).Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddMonthlySheet(XLWorkbook workbook, ProductPerformanceResponse summary)
    {
        var sheet = workbook.Worksheets.Add("按月趋势");
        WriteTable(
            sheet,
            [
                "Product ID", "标签", "月份", "订单数", "件数", "实际支付", "TikTok折扣", "预估可回款", "成交额"
            ],
            summary.Products.SelectMany(product => product.Monthly.Select(month => new object[]
            {
                product.ProductId,
                product.Label,
                month.Month,
                month.OrderCount,
                month.Quantity,
                month.PaidAmount,
                month.TikTokDiscountAmount,
                month.EstimatedReceivableAmount,
                month.GrossWithDiscount
            })).ToList());
    }

    private static void AddDailySheet(XLWorkbook workbook, ProductPerformanceResponse summary)
    {
        var sheet = workbook.Worksheets.Add("按日趋势");
        WriteTable(
            sheet,
            [
                "Product ID", "标签", "日期", "订单数", "件数", "实际支付", "TikTok折扣", "预估可回款", "成交额"
            ],
            summary.Products.SelectMany(product => product.Daily.Select(day => new object[]
            {
                product.ProductId,
                product.Label,
                day.Date,
                day.OrderCount,
                day.Quantity,
                day.PaidAmount,
                day.TikTokDiscountAmount,
                day.EstimatedReceivableAmount,
                day.GrossWithDiscount
            })).ToList());
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
        for (var index = 0; index < headers.Count; index += 1)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex += 1)
        {
            for (var cellIndex = 0; cellIndex < rows[rowIndex].Length; cellIndex += 1)
            {
                sheet.Cell(rowIndex + 2, cellIndex + 1).Value = XLCellValue.FromObject(rows[rowIndex][cellIndex]);
            }
        }

        sheet.Row(1).Style.Font.SetBold();
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents();
    }
}
