using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

public sealed class Quote
{
    public string Code;
    public string Name;
    public double Price;
    public double PrevClose;
    public double Pct;
}

public static class QuoteParser
{
    private static readonly Regex EntryRegex =
        new Regex("v_(?:sh|sz|bj)(\\d+)=\"([^\"]*)\"", RegexOptions.Compiled);

    public static Dictionary<string, Quote> Parse(string responseText)
    {
        var result = new Dictionary<string, Quote>();
        if (string.IsNullOrEmpty(responseText)) return result;
        foreach (Match m in EntryRegex.Matches(responseText))
        {
            string[] fields = m.Groups[2].Value.Split('~');
            if (fields.Length < 33) continue;
            var q = new Quote();
            q.Code = m.Groups[1].Value;
            q.Name = fields[1];
            q.Price = double.Parse(fields[3], CultureInfo.InvariantCulture);
            q.PrevClose = double.Parse(fields[4], CultureInfo.InvariantCulture);
            q.Pct = double.Parse(fields[32], CultureInfo.InvariantCulture);
            result[q.Code] = q;
        }
        return result;
    }

    public static string FormatPct(double pct)
    {
        return pct.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
    }
}
