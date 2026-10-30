using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
}
