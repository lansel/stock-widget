using System;

public enum MarketStatus
{
    Open,
    Lunch,
    Closed,
    Weekend
}

public static class TradingClock
{
    private static readonly TimeSpan OpenStart = new TimeSpan(9, 30, 0);
    private static readonly TimeSpan LunchStart = new TimeSpan(11, 30, 0);
    private static readonly TimeSpan AfternoonStart = new TimeSpan(13, 0, 0);
    private static readonly TimeSpan CloseTime = new TimeSpan(15, 0, 0);

    public static MarketStatus GetStatus(DateTime now)
    {
        DayOfWeek day = now.DayOfWeek;
        if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)
            return MarketStatus.Weekend;
        // 法定假日（工作日）休市；调休上班的周末已在上面的周末分支处理
        if (HolidayCalendar.IsClosed(now))
            return MarketStatus.Closed;

        TimeSpan t = now.TimeOfDay;
        if (t >= OpenStart && t < LunchStart) return MarketStatus.Open;
        if (t >= LunchStart && t < AfternoonStart) return MarketStatus.Lunch;
        if (t >= AfternoonStart && t < CloseTime) return MarketStatus.Open;
        return MarketStatus.Closed;
    }

    // 周末或法定假日（工作日）——当天完全不交易的“非开盘日”
    // 注意：调休上班的周六周日 A 股仍休市，也归入非开盘日
    public static bool IsNonTradingDay(DateTime now)
    {
        DayOfWeek day = now.DayOfWeek;
        if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday) return true;
        return HolidayCalendar.IsClosed(now);
    }

    public static int GetIntervalMs(MarketStatus status)
    {
        return status == MarketStatus.Open ? 3000 : 60000;
    }

    public static string GetLabel(MarketStatus status)
    {
        switch (status)
        {
            case MarketStatus.Open: return "开盘中";
            case MarketStatus.Lunch: return "午休";
            case MarketStatus.Weekend: return "周末";
            default: return "已收盘";
        }
    }
}
