using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--fetch")
            return RunFetch(args);

        string configPath = StockList.ResolveConfigPath(args);
        Application.EnableVisualStyles();
        Application.Run(new WidgetForm(configPath));
        return 0;
    }

    static int RunFetch(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法: StockWidget.exe --fetch <代码1> [代码2] ...");
            return 1;
        }
        var codes = args.Skip(1).ToArray();
        try
        {
            var symbols = codes.Select(StockList.ToSymbol).ToArray();
            string raw = QuoteClient.FetchRaw(symbols);
            var quotes = QuoteParser.Parse(raw);
            foreach (string code in codes)
            {
                Quote q;
                if (quotes.TryGetValue(code, out q))
                    Console.WriteLine(string.Format("{0} {1} {2} {3}",
                        code, q.Name, q.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                        QuoteParser.FormatPct(q.Pct)));
                else
                    Console.WriteLine(code + " 无数据");
            }
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("网络异常: " + e.Message);
            return 1;
        }
    }
}
