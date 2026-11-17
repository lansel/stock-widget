using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public enum StockListAddResult
{
    Added,
    AlreadyExists,
    Failed
}

public enum StockListRemoveResult
{
    Removed,
    NotFound,
    Failed
}

public static class StockListWriter
{
    public static string ToConfigCode(StockSearchResult result)
    {
        if (result == null) return "";
        if (StockList.ToSymbol(result.Code) == result.Symbol) return result.Code;
        return result.Symbol;
    }

    public static StockListAddResult Add(string path, StockSearchResult result)
    {
        if (result == null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(result.Symbol))
            return StockListAddResult.Failed;

        try
        {
            string existing = File.Exists(path) ? File.ReadAllText(path) : "";
            WidgetSettings settings = StockList.ParseWidgetSettings(existing);
            var known = new HashSet<string>();
            for (int i = 0; i < settings.Codes.Count; i++)
                known.Add(StockList.ToSymbol(settings.Codes[i]));

            if (known.Contains(result.Symbol))
                return StockListAddResult.AlreadyExists;

            string code = ToConfigCode(result);
            if (code.Length == 0) return StockListAddResult.Failed;

            string prefix = "";
            if (existing.Length > 0 && existing[existing.Length - 1] != '\n')
                prefix = Environment.NewLine;

            File.AppendAllText(path, prefix + code + Environment.NewLine,
                new UTF8Encoding(false));
            return StockListAddResult.Added;
        }
        catch
        {
            return StockListAddResult.Failed;
        }
    }

    public static StockListRemoveResult Remove(string path, string symbol)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(symbol))
            return StockListRemoveResult.Failed;
        symbol = symbol.Trim().ToLowerInvariant();

        try
        {
            byte[] data = File.ReadAllBytes(path);
            int bomLength = 0;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                bomLength = 3;

            var output = new List<byte>(data.Length);
            for (int i = 0; i < bomLength; i++) output.Add(data[i]);

            bool removed = false;
            int start = bomLength;
            for (int i = bomLength; i <= data.Length; i++)
            {
                if (i != data.Length && data[i] != (byte)'\n') continue;

                int contentEnd = i;
                if (contentEnd > start && data[contentEnd - 1] == (byte)'\r') contentEnd--;
                string line = AsciiOf(data, start, contentEnd);
                if (IsStockLine(line, symbol))
                {
                    removed = true;
                }
                else
                {
                    int end = i < data.Length ? i + 1 : i;
                    for (int k = start; k < end; k++) output.Add(data[k]);
                }
                start = i < data.Length ? i + 1 : i;
            }

            if (!removed) return StockListRemoveResult.NotFound;
            File.WriteAllBytes(path, output.ToArray());
            return StockListRemoveResult.Removed;
        }
        catch
        {
            return StockListRemoveResult.Failed;
        }
    }

    private static string AsciiOf(byte[] data, int start, int end)
    {
        var chars = new char[end - start];
        for (int i = start; i < end; i++)
        {
            byte b = data[i];
            chars[i - start] = b < 0x80 ? (char)b : '?';
        }
        return new string(chars);
    }

    private static bool IsStockLine(string line, string symbol)
    {
        line = line.Trim();
        int hash = line.IndexOf('#');
        if (hash >= 0) line = line.Substring(0, hash).TrimEnd();
        if (line.Length == 0) return false;
        if (line.IndexOf('=') > 0) return false;
        string[] parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        string code = parts[0];
        if (!StockList.IsCode(code)) return false;
        return StockList.ToSymbol(code) == symbol;
    }
}
