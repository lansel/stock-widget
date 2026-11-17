using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public sealed class StockSearchResult
{
    public string Market;
    public string Code;
    public string Name;
    public string Type;

    public string Symbol
    {
        get { return Market + Code; }
    }
}

public static class StockSearchParser
{
    public const int MaxResults = 10;

    private static readonly Regex HintRegex =
        new Regex("v_hint\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex CodeRegex = new Regex(@"^\d{6}$", RegexOptions.Compiled);
    private static readonly HashSet<string> TypeWhitelist =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GP-A", "GP-B", "ZS", "ETF", "LOF"
        };

    public static List<StockSearchResult> Parse(string responseText)
    {
        var results = new List<StockSearchResult>();
        if (string.IsNullOrEmpty(responseText)) return results;

        Match match = HintRegex.Match(responseText);
        if (!match.Success) return results;
        string payload = match.Groups[1].Value.Trim();
        if (payload.Length == 0 || payload == "N") return results;

        foreach (string rawEntry in payload.Split('^'))
        {
            string[] fields = rawEntry.Split('~');
            if (fields.Length < 5) continue;

            string market = fields[0].Trim();
            string code = fields[1].Trim();
            string name = Unescape(fields[2].Trim());
            string type = fields[4].Trim();

            if (!market.Equals("sh", StringComparison.OrdinalIgnoreCase)
                && !market.Equals("sz", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!CodeRegex.IsMatch(code)) continue;
            if (name.Length == 0) continue;
            if (!TypeWhitelist.Contains(type)) continue;

            var result = new StockSearchResult();
            result.Market = market.ToLowerInvariant();
            result.Code = code;
            result.Name = name;
            result.Type = type;
            results.Add(result);
            if (results.Count >= MaxResults) break;
        }
        return results;
    }

    public static string TypeLabel(string type)
    {
        if (string.Equals(type, "GP-A", StringComparison.OrdinalIgnoreCase)) return "股票";
        if (string.Equals(type, "GP-B", StringComparison.OrdinalIgnoreCase)) return "B股";
        if (string.Equals(type, "ZS", StringComparison.OrdinalIgnoreCase)) return "指数";
        if (string.Equals(type, "ETF", StringComparison.OrdinalIgnoreCase)) return "ETF";
        if (string.Equals(type, "LOF", StringComparison.OrdinalIgnoreCase)) return "LOF";
        return type;
    }

    private static string Unescape(string value)
    {
        if (value.IndexOf("\\u", StringComparison.Ordinal) < 0) return value;
        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 5 < value.Length && value[i + 1] == 'u')
            {
                int code;
                if (int.TryParse(value.Substring(i + 2, 4), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out code))
                {
                    sb.Append((char)code);
                    i += 5;
                    continue;
                }
            }
            sb.Append(value[i]);
        }
        return sb.ToString();
    }
}
