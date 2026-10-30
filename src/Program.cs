using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--fetch")
            return RunFetch(args);

        bool created;
        using (var mutex = new Mutex(true, @"Local\StockWidget_SingleInstance", out created))
        {
            if (!created)
            {
                return 0;
            }
            string configPath = StockList.ResolveConfigPath(args);
            WidgetSettings boot = StockList.LoadSettingsOrDefault(configPath);
            if (boot.Autostart) Autostart.Ensure(args);
            else Autostart.Remove();
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            Application.Run(new WidgetForm(configPath));
            GC.KeepAlive(mutex);
        }
        return 0;
    }

    static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        LogUnhandled(e.Exception, "ui");
    }

    static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogUnhandled(e.ExceptionObject as Exception, "appdomain");
    }

    // 兜底：未处理异常记到 %APPDATA%\StockWidget\error.log 后继续运行，挂件不闪退
    static void LogUnhandled(Exception ex, string source)
    {
        if (ex == null) return;
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StockWidget");
            Directory.CreateDirectory(dir);
            string logPath = Path.Combine(dir, "error.log");
            FileInfo fi = new FileInfo(logPath);
            if (fi.Exists && fi.Length > 1024 * 1024) fi.Delete();
            File.AppendAllText(logPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + source + "] " + ex + Environment.NewLine);
        }
        catch { }
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
                string symbol = StockList.ToSymbol(code);
                Quote q;
                if (quotes.TryGetValue(symbol, out q))
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
