using TikTokOrderPrinter.Models;

namespace TikTokOrderPrinter.Services;

public static class OrderPrintEligibility
{
    public static bool IsAwaitingShipment(string? status) =>
        string.Equals(status, "AWAITING_SHIPMENT", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "TO_SHIP", StringComparison.OrdinalIgnoreCase);

    public static bool IsCancelled(string? status) =>
        string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase);

    public static bool IsPaid(DateTimeOffset? paidAtUtc) =>
        paidAtUtc is not null;

    public static bool IsForToday(DateTimeOffset? paidAtUtc, DateTimeOffset? nowLocal = null)
    {
        if (paidAtUtc is null)
        {
            return false;
        }

        var localNow = nowLocal ?? DateTimeOffset.Now;
        return paidAtUtc.Value.ToLocalTime().Date == localNow.Date;
    }

    public static bool CanAutoPrint(OrderPrintModel order) =>
        IsAwaitingShipment(order.Status) &&
        IsPaid(order.PaidAtUtc) &&
        IsForToday(order.PaidAtUtc);

    public static bool CanAutoPrint(PrintedOrderRecord record) =>
        IsAwaitingShipment(record.Status) &&
        IsPaid(record.PaidAtUtc) &&
        IsForToday(record.PaidAtUtc);

    public static bool CanStandardPrint(PrintedOrderRecord? record) =>
        record is not null &&
        record.PrintedAtUtc is null &&
        CanAutoPrint(record);

    public static string GetBlockingReason(string? status, DateTimeOffset? paidAtUtc, bool alreadyPrinted)
    {
        if (alreadyPrinted)
        {
            return "这笔订单已经打印过，如需补打请使用“重新打印”。";
        }

        if (IsCancelled(status))
        {
            return "已取消订单不会进入打印。";
        }

        if (!IsPaid(paidAtUtc))
        {
            return "未支付订单不会进入打印。";
        }

        if (!IsForToday(paidAtUtc))
        {
            return "只会打印当天已支付的待发货订单。";
        }

        if (!IsAwaitingShipment(status))
        {
            return "只有待发货订单才能直接打印。";
        }

        return "当前订单不符合打印条件。";
    }
}
