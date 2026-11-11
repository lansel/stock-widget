using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public sealed class MinutePoint
{
    public string Time;
    public int MinuteIndex;
    public double Price;
    public double CumVolume;
    public double CumAmount;
    public double Avg;
}

public sealed class ChartQuote
{
    public string Name;
    public double Price;
    public double PrevClose;
    public double Pct;
}

public sealed class MinuteSeries
{
    public string Symbol;
    public string Date;
    public bool HasAvg;
    public ChartQuote Quote;
    public List<MinutePoint> Points = new List<MinutePoint>();
}

public sealed class DailyBar
{
    public string Date;
    public double Open;
    public double Close;
    public double High;
    public double Low;
    public double Volume;
}

public sealed class DailySeries
{
    public string Symbol;
    public ChartQuote Quote;
    public List<DailyBar> Bars = new List<DailyBar>();
}

public static class ChartParser
{
    public static int TimeToMinuteIndex(string hhmm)
    {
        if (string.IsNullOrEmpty(hhmm) || hhmm.Length != 4) return -1;
        int hh, mm;
        if (!int.TryParse(hhmm.Substring(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out hh)) return -1;
        if (!int.TryParse(hhmm.Substring(2, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out mm)) return -1;
        int t = hh * 100 + mm;
        if (t >= 930 && t <= 1130) return (hh - 9) * 60 + (mm - 30);
        if (t >= 1300 && t <= 1500) return 120 + (hh - 13) * 60 + mm;
        return -1;
    }

    public static bool IsIndexSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return false;
        string s = symbol.ToLowerInvariant();
        return s.StartsWith("sh000") || s.StartsWith("sz399") || s.StartsWith("bj899");
    }

    public static MinuteSeries ParseMinute(string symbol, string json)
    {
        var result = new MinuteSeries();
        result.Symbol = symbol ?? "";
        result.HasAvg = !IsIndexSymbol(result.Symbol);
        if (string.IsNullOrEmpty(json) || result.Symbol.Length == 0) return result;

        int sym = FindSymbolNode(json, result.Symbol);
        if (sym < 0) return result;

        int dataKey = json.IndexOf("\"data\":[", sym, StringComparison.Ordinal);
        if (dataKey >= 0)
        {
            int pos = json.IndexOf('[', dataKey);
            List<string> rows;
            if (ReadStringArray(json, ref pos, out rows))
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    MinutePoint p = ParseMinutePoint(rows[i], result.HasAvg);
                    if (p != null) result.Points.Add(p);
                }
            }
        }

        result.Date = ReadStringValue(json, "\"date\":\"", sym);
        result.Quote = ParseQuote(json, result.Symbol, sym);
        if (result.Quote == null) result.Quote = new ChartQuote();
        if (result.Quote.Price <= 0 && result.Points.Count > 0)
            result.Quote.Price = result.Points[result.Points.Count - 1].Price;
        if (result.Quote.PrevClose <= 0 && result.Points.Count > 0)
            result.Quote.PrevClose = result.Points[0].Price;
        return result;
    }

    public static DailySeries ParseDaily(string symbol, string json)
    {
        return ParseKline(symbol, json, "day");
    }

    public static DailySeries ParseKline(string symbol, string json, string period)
    {
        if (period != "week" && period != "month") period = "day";
        var result = new DailySeries();
        result.Symbol = symbol ?? "";
        if (string.IsNullOrEmpty(json) || result.Symbol.Length == 0) return result;

        int sym = FindSymbolNode(json, result.Symbol);
        if (sym < 0) return result;

        string qfqMarker = "\"qfq" + period + "\":[";
        int qfq = json.IndexOf(qfqMarker, sym, StringComparison.Ordinal);
        if (qfq >= 0)
            ReadBarArrays(json, qfq + qfqMarker.Length - 1, result.Bars);
        if (result.Bars.Count == 0)
        {
            string marker = "\"" + period + "\":[";
            int plain = json.IndexOf(marker, sym, StringComparison.Ordinal);
            if (plain >= 0) ReadBarArrays(json, plain + marker.Length - 1, result.Bars);
        }

        result.Quote = ParseQuote(json, result.Symbol, sym);
        if (result.Quote == null) result.Quote = new ChartQuote();
        if (result.Quote.Price <= 0 && result.Bars.Count > 0)
            result.Quote.Price = result.Bars[result.Bars.Count - 1].Close;
        if (result.Quote.PrevClose <= 0 && result.Bars.Count > 1)
            result.Quote.PrevClose = result.Bars[result.Bars.Count - 2].Close;
        return result;
    }

    private static MinutePoint ParseMinutePoint(string row, bool hasAvg)
    {
        if (string.IsNullOrEmpty(row)) return null;
        string[] parts = row.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return null;
        int index = TimeToMinuteIndex(parts[0]);
        if (index < 0) return null;
        double price, volume, amount = 0;
        if (!TryParseDouble(parts[1], out price)) return null;
        if (!TryParseDouble(parts[2], out volume)) return null;
        if (parts.Length > 3) TryParseDouble(parts[3], out amount);

        var p = new MinutePoint();
        p.Time = parts[0];
        p.MinuteIndex = index;
        p.Price = price;
        p.CumVolume = volume;
        p.CumAmount = amount;
        p.Avg = price;
        if (hasAvg && volume > 0 && amount > 0)
            p.Avg = amount / (volume * 100.0);
        return p;
    }

    private static DailyBar ParseDailyBar(List<string> row)
    {
        if (row == null || row.Count < 6) return null;
        double open, close, high, low, volume;
        if (!TryParseDouble(row[1], out open)) return null;
        if (!TryParseDouble(row[2], out close)) return null;
        if (!TryParseDouble(row[3], out high)) return null;
        if (!TryParseDouble(row[4], out low)) return null;
        if (!TryParseDouble(row[5], out volume)) return null;
        if (open <= 0 || close <= 0 || high <= 0 || low <= 0) return null;

        var b = new DailyBar();
        b.Date = row[0];
        b.Open = open;
        b.Close = close;
        b.High = high;
        b.Low = low;
        b.Volume = volume;
        return b;
    }

    private static ChartQuote ParseQuote(string json, string symbol, int from)
    {
        int qt = json.IndexOf("\"qt\":{", from, StringComparison.Ordinal);
        if (qt < 0) return null;
        int key = json.IndexOf("\"" + symbol + "\":[", qt, StringComparison.Ordinal);
        if (key < 0) return null;

        int pos = key + symbol.Length + 3;
        List<string> values;
        if (!ReadStringArray(json, ref pos, out values)) return null;
        if (values.Count < 5) return null;

        var q = new ChartQuote();
        q.Name = values.Count > 1 ? values[1] : "";
        if (values.Count > 3) TryParseDouble(values[3], out q.Price);
        if (values.Count > 4) TryParseDouble(values[4], out q.PrevClose);
        double pct;
        if (values.Count > 32 && TryParseDouble(values[32], out pct))
            q.Pct = pct;
        else if (q.PrevClose > 0)
            q.Pct = (q.Price / q.PrevClose - 1) * 100;
        return q;
    }

    private static void ReadBarArrays(string json, int pos, List<DailyBar> bars)
    {
        if (pos < 0 || pos >= json.Length || json[pos] != '[') return;
        int i = pos + 1;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == ']') return;
            if (c == ',') { i++; continue; }
            if (c == '[')
            {
                List<string> row;
                if (!ReadStringArray(json, ref i, out row)) return;
                DailyBar bar = ParseDailyBar(row);
                if (bar != null) bars.Add(bar);
                continue;
            }
            i++;
        }
    }

    private static int FindSymbolNode(string json, string symbol)
    {
        return json.IndexOf("\"" + symbol + "\":{", StringComparison.Ordinal);
    }

    private static string ReadStringValue(string json, string marker, int from)
    {
        int key = json.IndexOf(marker, from, StringComparison.Ordinal);
        if (key < 0) return "";
        int start = key + marker.Length;
        int end = json.IndexOf('"', start);
        if (end <= start) return "";
        return json.Substring(start, end - start);
    }

    private static bool ReadStringArray(string json, ref int pos, out List<string> values)
    {
        values = new List<string>();
        if (pos < 0 || pos >= json.Length || json[pos] != '[') return false;
        int i = pos + 1;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == ']')
            {
                pos = i + 1;
                return true;
            }
            if (c == ',') { i++; continue; }
            if (c != '"') { i++; continue; }

            var sb = new StringBuilder();
            i++;
            while (i < json.Length)
            {
                char ch = json[i];
                if (ch == '\\' && i + 1 < json.Length)
                {
                    char esc = json[i + 1];
                    if (esc == 'u' && i + 5 < json.Length)
                    {
                        int code;
                        if (int.TryParse(json.Substring(i + 2, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 6;
                            continue;
                        }
                    }
                    if (esc == 'n') sb.Append('\n');
                    else if (esc == 'r') sb.Append('\r');
                    else if (esc == 't') sb.Append('\t');
                    else sb.Append(esc);
                    i += 2;
                    continue;
                }
                if (ch == '"')
                {
                    i++;
                    break;
                }
                sb.Append(ch);
                i++;
            }
            values.Add(sb.ToString());
        }
        return false;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
