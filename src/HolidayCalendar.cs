using System;

public static class HolidayCalendar
{
    // A 股 2026 年非周末休市日，来源：上交所《关于2026年部分节假日休市安排的通知》
    // https://www.sse.com.cn/disclosure/announcement/general/c/c_20251222_10802507.shtml
    // 新年度安排公布后随版本更新；未覆盖年份退回周末/时段判断，行为与旧版一致
    private static readonly string[] Closed2026 =
    {
        "20260101", "20260102",                                      // 元旦
        "20260216", "20260217", "20260218", "20260219", "20260220",  // 春节
        "20260223",
        "20260406",                                                  // 清明节
        "20260501", "20260504", "20260505",                          // 劳动节
        "20260619",                                                  // 端午节
        "20260925",                                                  // 中秋节
        "20261001", "20261002", "20261005", "20261006", "20261007",  // 国庆节
    };

    public static bool IsClosed(DateTime date)
    {
        if (date.Year != 2026) return false;
        string key = date.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        return Array.IndexOf(Closed2026, key) >= 0;
    }
}
