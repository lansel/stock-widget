using System;
using System.Collections.Generic;
using System.Globalization;

public static class ChartScale
{
    public static List<double> NiceTicks(double min, double max, int targetCount)
    {
        var ticks = new List<double>();
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max))
            return ticks;
        if (targetCount < 1) targetCount = 1;
        if (max < min)
        {
            double swap = min;
            min = max;
            max = swap;
        }
        if (max - min < 1e-12)
        {
            double pad = Math.Abs(max) * 0.01;
            if (pad < 1e-9) pad = 0.01;
            min -= pad;
            max += pad;
        }

        double raw = (max - min) / targetCount;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / magnitude;
        double step;
        if (norm <= 1.5) step = magnitude;
        else if (norm <= 3) step = 2 * magnitude;
        else if (norm <= 7) step = 5 * magnitude;
        else step = 10 * magnitude;

        double first = Math.Ceiling(min / step - 1e-9) * step;
        for (double v = first; v <= max + step * 1e-9; v += step)
        {
            ticks.Add(Math.Round(v, 10));
            if (ticks.Count > 1000) break;
        }
        return ticks;
    }

    public static double[] MovingAverage(IList<DailyBar> bars, int period)
    {
        if (bars == null) return new double[0];
        var result = new double[bars.Count];
        if (period <= 0)
        {
            for (int i = 0; i < result.Length; i++) result[i] = double.NaN;
            return result;
        }
        double sum = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            sum += bars[i].Close;
            if (i >= period) sum -= bars[i - period].Close;
            result[i] = i >= period - 1 ? sum / period : double.NaN;
        }
        return result;
    }

    public static int VisibleStart(int total, int visible)
    {
        if (visible <= 0 || total <= 0) return 0;
        int start = total - visible;
        return start > 0 ? start : 0;
    }

    public static double PercentFromPrev(double price, double prevClose)
    {
        if (prevClose <= 0) return 0;
        return (price / prevClose - 1) * 100;
    }

    public static string FormatShortDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "";
        if (isoDate.Length >= 10) return isoDate.Substring(5, 5);
        return isoDate;
    }

    public static string FormatWeek(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "";
        if (isoDate.Length >= 10) return isoDate.Substring(2, 8);
        return isoDate;
    }

    public static string FormatMonth(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "";
        if (isoDate.Length >= 7) return isoDate.Substring(0, 7);
        return isoDate;
    }

    public static double IntradayMa(IList<DailyBar> bars, string tradeDate,
        double currentPrice, int period)
    {
        if (bars == null || period <= 0 || currentPrice <= 0) return double.NaN;
        string today = Digits(tradeDate);
        int need = period - 1;
        double sum = 0;
        int count = 0;
        for (int i = bars.Count - 1; i >= 0 && count < need; i--)
        {
            string date = Digits(bars[i].Date);
            if (today.Length > 0 && string.CompareOrdinal(date, today) >= 0) continue;
            sum += bars[i].Close;
            count++;
        }
        if (count < need) return double.NaN;
        return (sum + currentPrice) / period;
    }

    private static string Digits(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("-", "").Replace("/", "");
    }

    public static double MaxVolume(IList<DailyBar> bars, int start)
    {
        if (bars == null || bars.Count == 0) return 0;
        if (start < 0) start = 0;
        double max = 0;
        for (int i = start; i < bars.Count; i++)
        {
            double volume = bars[i].Volume;
            if (volume > max) max = volume;
        }
        return max;
    }

    public static double[] MinuteVolumes(IList<MinutePoint> points)
    {
        var result = new double[241];
        if (points == null) return result;
        double previous = 0;
        for (int i = 0; i < points.Count; i++)
        {
            MinutePoint p = points[i];
            double volume = p.CumVolume - previous;
            if (volume < 0) volume = 0;
            if (p.MinuteIndex >= 0 && p.MinuteIndex <= 240)
                result[p.MinuteIndex] += volume;
            if (p.CumVolume > previous) previous = p.CumVolume;
        }
        return result;
    }

    public static string FormatVolume(double volume)
    {
        if (double.IsNaN(volume) || double.IsInfinity(volume)) return "0";
        double abs = Math.Abs(volume);
        if (abs >= 100000000)
            return (volume / 100000000).ToString("0.00", CultureInfo.InvariantCulture) + "亿";
        if (abs >= 10000)
        {
            double wan = volume / 10000.0;
            if (Math.Abs(wan) >= 999.5)
                return wan.ToString("0", CultureInfo.InvariantCulture) + "万";
            return wan.ToString("0.0", CultureInfo.InvariantCulture) + "万";
        }
        if (abs >= 100)
            return volume.ToString("0", CultureInfo.InvariantCulture);
        return volume.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static double LimitRatio(string symbol, string name)
    {
        if (string.IsNullOrEmpty(symbol)) return 0;
        string s = symbol.ToLowerInvariant();
        if (s.StartsWith("sh000") || s.StartsWith("sz399") || s.StartsWith("bj899")) return 0;
        if (s.StartsWith("sh600") || s.StartsWith("sh601") || s.StartsWith("sh603") || s.StartsWith("sh605")
            || s.StartsWith("sh900") || s.StartsWith("sz000") || s.StartsWith("sz001")
            || s.StartsWith("sz002") || s.StartsWith("sz003") || s.StartsWith("sz200"))
            return IsStName(name) ? 0.05 : 0.10;
        if (s.StartsWith("sh688") || s.StartsWith("sh689")
            || s.StartsWith("sz300") || s.StartsWith("sz301")) return 0.20;
        if (s.StartsWith("bj")) return 0.30;
        return 0;
    }

    public static bool IsLimitUp(DailyBar bar, DailyBar prev, double limitRatio)
    {
        if (bar == null || prev == null || limitRatio <= 0) return false;
        if (prev.Close <= 0) return false;
        double pct = (bar.Close / prev.Close - 1) * 100;
        double limitPct = limitRatio * 100;
        double tolerance = 0.2 + 0.01 * limitPct;
        if (Math.Abs(pct - limitPct) > tolerance) return false;
        double epsilon = Math.Max(0.0005, prev.Close * 0.0001);
        return Math.Abs(bar.Close - bar.High) <= epsilon;
    }

    private static bool IsStName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.Trim().ToUpperInvariant();
        return n.StartsWith("ST") || n.StartsWith("*ST");
    }
}
