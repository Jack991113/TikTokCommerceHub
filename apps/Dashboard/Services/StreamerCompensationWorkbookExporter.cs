using ClosedXML.Excel;
using TikTokSalesStats.Models;

namespace TikTokSalesStats.Services;

public sealed class StreamerCompensationWorkbookExporter
{
    public byte[] BuildWorkbook(StreamerCompensationResponse summary)
    {
        using var workbook = new XLWorkbook();

        AddOverviewSheet(workbook, summary);
        AddStreamerSheet(workbook, summary);
        AddMonthlyProfitSheet(workbook, summary);
        AddStoreBreakdownSheet(workbook, summary);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddOverviewSheet(XLWorkbook workbook, StreamerCompensationResponse summary)
    {
        var sheet = workbook.Worksheets.Add("主播总览");
        var totals = summary.Totals;
        var rows = new (string Label, object Value)[]
        {
            ("店铺", summary.StoreName),
            ("统计区间", $"{summary.FromUtc:yyyy-MM-dd} ~ {summary.ToUtc:yyyy-MM-dd}"),
            ("生成时间", summary.GeneratedAtUtc.LocalDateTime),
            ("币种", summary.Currency),
            ("隐藏采购成本(JPY)", summary.HiddenProcurementCostJpy),
            ("跟踪产品数", totals.ProductCount),
            ("订单数", totals.OrderCount),
            ("件数", totals.Quantity),
            ("实际支付", totals.PaidAmount),
            ("TikTok 折扣", totals.TikTokDiscountAmount),
            ("买家运费", totals.BuyerShippingFeeAmount),
            ("平台费", totals.EstimatedPlatformFeeAmount),
            ("物流成本", totals.EstimatedLogisticsCostAmount),
            ("预估可回款", totals.EstimatedReceivableAmount),
            ("已回款", totals.EstimatedSettledReceivableAmount),
            ("未回款", totals.EstimatedPendingReceivableAmount),
            ("底薪(RMB)", totals.SalaryBaseAmountRmb),
            ("提成(RMB)", totals.SalaryCommissionAmountRmb),
            ("薪资合计(RMB)", totals.SalaryTotalAmountRmb),
            ("隐形成本(JPY)", totals.HiddenProcurementCostJpy),
            ("隐形成本前利润(JPY)", totals.ProfitBeforeHiddenCostJpy),
            ("隐形成本后利润(JPY)", totals.ProfitAfterHiddenCostJpy),
            ("回款完成率", totals.SettlementCompletionRate / 100m)
        };

        WriteKeyValueSheet(sheet, "主播薪资与利润", rows);
        sheet.Cell($"B{rows.Length + 2}").Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddStreamerSheet(XLWorkbook workbook, StreamerCompensationResponse summary)
    {
        var sheet = workbook.Worksheets.Add("主播汇总");
        var rows = summary.Streamers
            .Concat([summary.SelfOwned])
            .Select(item => new object[]
            {
                item.Label,
                item.IsSelfOwned ? "自营" : "主播",
                string.Join(", ", item.ProductIds.Select(product => product.ProductId)),
                item.OrderCount,
                item.Quantity,
                item.PaidAmount,
                item.TikTokDiscountAmount,
                item.BuyerShippingFeeAmount,
                item.EstimatedPlatformFeeAmount,
                item.EstimatedLogisticsCostAmount,
                item.EstimatedReceivableAmount,
                item.EstimatedSettledReceivableAmount,
                item.EstimatedPendingReceivableAmount,
                item.BaseSalaryAmount,
                item.BaseSalaryCurrency,
                item.CommissionAmountRmb,
                item.SalaryTotalAmountRmb,
                item.CommissionRate,
                item.CommissionAmountJpy,
                item.SalaryTotalAmountJpy,
                item.AllocatedHiddenProcurementCostJpy,
                item.ProfitBeforeHiddenCostJpy,
                item.ProfitAfterHiddenCostJpy,
                item.SettlementCompletionRate / 100m,
                item.Note
            })
            .ToList();

        WriteTable(
            sheet,
            [
                "主体", "类型", "Product IDs", "订单数", "件数", "实际支付", "TikTok折扣", "买家运费", "平台费", "物流成本",
                "预估可回款", "已回款", "未回款", "底薪RMB", "底薪币种", "提成RMB", "薪资合计RMB", "提成比例", "提成JPY", "薪资合计JPY",
                "分摊隐形成本JPY", "隐形成本前利润JPY", "隐形成本后利润JPY", "回款完成率", "备注"
            ],
            rows);

        sheet.Column(24).Style.NumberFormat.SetFormat("0.0%");
    }

    private static void AddMonthlyProfitSheet(XLWorkbook workbook, StreamerCompensationResponse summary)
    {
        var sheet = workbook.Worksheets.Add("月度利润");
        WriteTable(
            sheet,
            [
                "月份", "实际支付", "TikTok折扣", "预估可回款", "底薪RMB", "提成RMB", "薪资合计RMB", "隐形成本JPY", "隐形成本前利润JPY", "隐形成本后利润JPY"
            ],
            summary.MonthlyProfit.Select(item => new object[]
            {
                item.Month,
                item.PaidAmount,
                item.TikTokDiscountAmount,
                item.EstimatedReceivableAmount,
                item.SalaryBaseAmountRmb,
                item.SalaryCommissionAmountRmb,
                item.SalaryTotalAmountRmb,
                item.HiddenProcurementCostJpy,
                item.ProfitBeforeHiddenCostJpy,
                item.ProfitAfterHiddenCostJpy
            }).ToList());
    }

    private static void AddStoreBreakdownSheet(XLWorkbook workbook, StreamerCompensationResponse summary)
    {
        var sheet = workbook.Worksheets.Add("店铺拆分");
        WriteTable(
            sheet,
            [
                "主体", "店铺", "订单数", "件数", "实际支付", "TikTok折扣", "买家运费", "平台费", "物流成本", "预估可回款", "已回款", "未回款", "回款完成率"
            ],
            summary.Streamers
                .Concat([summary.SelfOwned])
                .SelectMany(item => item.StoreBreakdown.Select(store => new object[]
                {
                    item.Label,
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
                    store.SettlementCompletionRate / 100m
                }))
                .ToList());

        sheet.Column(13).Style.NumberFormat.SetFormat("0.0%");
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
