using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

public class WidgetSettings
{
    public List<string> Codes = new List<string>();
    public bool HideOnWinD = false;
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
            if (line.StartsWith("hideOnWinD", StringComparison.OrdinalIgnoreCase))
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                    s.HideOnWinD = line.Substring(eq + 1).Trim()
                        .Equals("true", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (Regex.IsMatch(line, @"^\d{6}$") && seen.Add(line))
                s.Codes.Add(line);
        }
        return s;
    }

    public static string ToSymbol(string code)
    {
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
