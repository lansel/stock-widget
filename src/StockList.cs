using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

public class WidgetSettings
{
    public List<string> Codes = new List<string>();
    public bool HideOnWinD = false;
    public double Opacity = 0.92;
    public Color BgColor = Color.FromArgb(32, 32, 36);
    public Color NameColor = Color.WhiteSmoke;
}

public static class StockList
{
    public static List<string> ParseFileContent(string content)
    {
        var result = new List<string>();
        if (content == null) return result;
        var seen = new HashSet<string>();
        content = content.TrimStart('\uFEFF');
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (!Regex.IsMatch(line, @"^\d{6}$")) continue;
            if (seen.Add(line)) result.Add(line);
        }
        return result;
    }

    public static WidgetSettings ParseWidgetSettings(string content)
    {
        var s = new WidgetSettings();
        if (content == null) return s;
        content = content.TrimStart('\uFEFF');
        var seen = new HashSet<string>();
        foreach (string raw in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            int eq = line.IndexOf('=');
            if (eq > 0)
            {
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                if (key.Equals("hideOnWinD", StringComparison.OrdinalIgnoreCase))
                {
                    s.HideOnWinD = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (key.Equals("opacity", StringComparison.OrdinalIgnoreCase))
                {
                    double v;
                    if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                        s.Opacity = Math.Max(0.1, Math.Min(1.0, v));
                    continue;
                }
                if (key.Equals("bgColor", StringComparison.OrdinalIgnoreCase))
                {
                    Color c;
                    if (TryParseColor(val, out c)) s.BgColor = c;
                    continue;
                }
                if (key.Equals("nameColor", StringComparison.OrdinalIgnoreCase))
                {
                    Color c;
                    if (TryParseColor(val, out c)) s.NameColor = c;
                    continue;
                }
            }

            if ((Regex.IsMatch(line, @"^\d{6}$") ||
                 Regex.IsMatch(line, @"^(sh|sz|bj)\d{6}$", RegexOptions.IgnoreCase) ||
                 Regex.IsMatch(line, @"^1A\d{4}$", RegexOptions.IgnoreCase))
                && seen.Add(line))
                s.Codes.Add(line);
        }
        return s;
    }

    private static bool TryParseColor(string val, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrEmpty(val)) return false;
        val = val.TrimStart('#');
        if (val.Length == 3)
            val = "" + val[0] + val[0] + val[1] + val[1] + val[2] + val[2];
        if (val.Length != 6) return false;
        int r, g, b;
        if (!int.TryParse(val.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r)) return false;
        if (!int.TryParse(val.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g)) return false;
        if (!int.TryParse(val.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) return false;
        color = Color.FromArgb(r, g, b);
        return true;
    }

    public static string ToSymbol(string code)
    {
        code = code.Trim();
        // 已带 sh/sz/bj 前缀的直接返回（小写）
        if (Regex.IsMatch(code, @"^(sh|sz|bj)\d{6}$", RegexOptions.IgnoreCase))
            return code.ToLower();
        // 指数代码：1A0001 → sh000001（上证指数）
        if (Regex.IsMatch(code, @"^1A\d{4}$", RegexOptions.IgnoreCase))
            return "sh00" + code.Substring(2);
        if (code.StartsWith("92")) return "bj" + code;
        char c = code[0];
        if (c == '6' || c == '9') return "sh" + code;
        if (c == '4' || c == '8') return "bj" + code;
        return "sz" + code;
    }

    public static string GetConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "stocks.txt");
    }

    public static string ResolveConfigPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config" && !string.IsNullOrEmpty(args[i + 1]))
                return args[i + 1];
        }
        try
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (!string.IsNullOrEmpty(exeDir))
            {
                string local = Path.Combine(exeDir, "stocks.txt");
                if (File.Exists(local)) return local;
            }
        }
        catch { }
        return GetConfigPath();
    }
}
