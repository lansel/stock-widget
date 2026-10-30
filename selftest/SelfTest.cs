using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

static class SelfTest
{
    static int Main()
    {
        int fail = 0;
        fail += Run("sanity", () => Assert(1 + 1 == 2));
        fail += Run("StockList.ParseFileContent basic", () => {
            var r = StockList.ParseFileContent("# c\n\n600519\r\n000001  \n");
            Assert(r.Count == 2 && r[0] == "600519" && r[1] == "000001");
        });
        fail += Run("StockList.ParseFileContent dedupe and invalid", () => {
            var r = StockList.ParseFileContent("600519\n600519\nabc\n12345\n000001");
            Assert(r.Count == 2 && r[0] == "600519" && r[1] == "000001");
        });
        fail += Run("StockList.ParseFileContent empty and BOM", () => {
            Assert(StockList.ParseFileContent("").Count == 0);
            Assert(StockList.ParseFileContent("\uFEFF600519").Count == 1);
        });
        fail += Run("StockList.ToSymbol markets", () => {
            Assert(StockList.ToSymbol("600519") == "sh600519");
            Assert(StockList.ToSymbol("688981") == "sh688981");
            Assert(StockList.ToSymbol("000001") == "sz000001");
            Assert(StockList.ToSymbol("300750") == "sz300750");
            Assert(StockList.ToSymbol("430047") == "bj430047");
            Assert(StockList.ToSymbol("830799") == "bj830799");
            Assert(StockList.ToSymbol("920002") == "bj920002");
            Assert(StockList.ToSymbol("900901") == "sh900901");
        });
        fail += Run("StockList.ToSymbol index and prefixed", () => {
            Assert(StockList.ToSymbol("1A0001") == "sh000001");
            Assert(StockList.ToSymbol("sh000001") == "sh000001");
            Assert(StockList.ToSymbol("SZ399001") == "sz399001");
        });
        fail += Run("StockList.GetConfigPath on desktop", () => {
            Assert(StockList.GetConfigPath().EndsWith("stocks.txt"));
        });
        fail += Run("StockList.ParseWidgetSettings codes only", () => {
            var s = StockList.ParseWidgetSettings("# comment\n600519\n000001\n");
            Assert(s.Codes.Count == 2 && s.Codes[0] == "600519" && !s.HideOnWinD);
        });
        fail += Run("StockList.ParseWidgetSettings hideOnWinD true", () => {
            var s = StockList.ParseWidgetSettings("600519\nhideOnWinD=true\n");
            Assert(s.Codes.Count == 1 && s.HideOnWinD);
        });
        fail += Run("StockList.ParseWidgetSettings hideOnWinD false", () => {
            var s = StockList.ParseWidgetSettings("hideOnWinD=false\n600519\n");
            Assert(s.Codes.Count == 1 && !s.HideOnWinD);
        });
        fail += Run("StockList.ParseWidgetSettings mixed", () => {
            var s = StockList.ParseWidgetSettings("# stocks\n600519\n\nhideOnWinD=true\n000001\n");
            Assert(s.Codes.Count == 2 && s.Codes[0] == "600519" && s.Codes[1] == "000001" && s.HideOnWinD);
        });
        fail += Run("StockList.ResolveConfigPath with --config", () => {
            var args = new[] { "--config", @"E:\test\stocks.txt" };
            Assert(StockList.ResolveConfigPath(args) == @"E:\test\stocks.txt");
        });
        fail += Run("PosConfig save load roundtrip", () => {
            string p = Path.Combine(Path.GetTempPath(), "pos_test.ini");
            PosConfig.Save(p, new Point(123, 456));
            Point r = PosConfig.Load(p);
            Assert(r.X == 123 && r.Y == 456);
            Assert(PosConfig.Load(Path.Combine(Path.GetTempPath(), "no_such_pos.ini")) == Point.Empty);
            File.Delete(p);
        });
        fail += Run("PosConfig.Clamp", () => {
            var area = new Rectangle(0, 0, 1920, 1040);
            Assert(PosConfig.Clamp(new Point(-50, 10), area) == new Point(0, 10));
            Assert(PosConfig.Clamp(new Point(10, -5), area) == new Point(10, 0));
            Assert(PosConfig.Clamp(new Point(5000, 5000), area) == new Point(1919, 1039));
            Assert(PosConfig.Clamp(new Point(100, 100), area) == new Point(100, 100));
        });
        fail += Run("QuoteParser.Parse multi entry", () => {
            string text = TEntry("sh600519", "600519", "\u8d35\u5dde\u8305\u53f0", "1369.04", "1.11")
                + ";\n" + TEntry("sz000001", "000001", "\u5e73\u5b89\u94f6\u884c", "11.47", "-0.61")
                + ";\nv_pv_none_match=\"1\";";
            var r = QuoteParser.Parse(text);
            Assert(r.Count == 2);
            Assert(r["sh600519"].Name == "\u8d35\u5dde\u8305\u53f0");
            Assert(r["sh600519"].Price == 1369.04 && r["sh600519"].Pct == 1.11);
            Assert(r["sz000001"].Pct == -0.61);
        });
        fail += Run("QuoteParser.Parse garbage and truncated", () => {
            Assert(QuoteParser.Parse("").Count == 0);
            Assert(QuoteParser.Parse("<html>error</html>").Count == 0);
            Assert(QuoteParser.Parse("v_sh600519=\"1~only~two\";").Count == 0);
        });
        fail += Run("QuoteParser.FormatPct", () => {
            Assert(QuoteParser.FormatPct(1.11) == "+1.11%");
            Assert(QuoteParser.FormatPct(-0.61) == "-0.61%");
            Assert(QuoteParser.FormatPct(0) == "0.00%");
            Assert(QuoteParser.FormatPct(2.5) == "+2.50%");
        });
        fail += Run("TradingClock boundaries", () => {
            var sat = new DateTime(2026, 10, 31, 10, 0, 0);
            var mon = new DateTime(2026, 11, 2, 0, 0, 0);
            Assert(TradingClock.GetStatus(sat) == MarketStatus.Weekend);
            Assert(TradingClock.GetStatus(mon.AddHours(8)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(mon.AddHours(9).AddMinutes(29)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(mon.AddHours(9).AddMinutes(30)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(mon.AddHours(11).AddMinutes(29)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(mon.AddHours(11).AddMinutes(30)) == MarketStatus.Lunch);
            Assert(TradingClock.GetStatus(mon.AddHours(12).AddMinutes(59)) == MarketStatus.Lunch);
            Assert(TradingClock.GetStatus(mon.AddHours(13)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(mon.AddHours(14).AddMinutes(59)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(mon.AddHours(15)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(mon.AddHours(16)) == MarketStatus.Closed);
        });
        fail += Run("TradingClock interval and label", () => {
            Assert(TradingClock.GetIntervalMs(MarketStatus.Open) == 3000);
            Assert(TradingClock.GetIntervalMs(MarketStatus.Lunch) == 60000);
            Assert(TradingClock.GetIntervalMs(MarketStatus.Closed) == 60000);
            Assert(TradingClock.GetIntervalMs(MarketStatus.Weekend) == 60000);
            Assert(TradingClock.GetLabel(MarketStatus.Open) == "\u5f00\u76d8\u4e2d");
            Assert(TradingClock.GetLabel(MarketStatus.Lunch) == "\u5348\u4f11");
            Assert(TradingClock.GetLabel(MarketStatus.Closed) == "\u5df2\u6536\u76d8");
            Assert(TradingClock.GetLabel(MarketStatus.Weekend) == "\u5468\u672b");
        });
        fail += Run("QuoteClient.BuildUrl", () => {
            Assert(QuoteClient.BuildUrl(new[] { "sh600519", "sz000001" }) == "https://qt.gtimg.cn/q=sh600519,sz000001");
        });
        fail += Run("QuoteClient.DecodeGbk roundtrip", () => {
            string s = "\u8d35\u5dde\u8305\u53f0";
            byte[] bytes = Encoding.GetEncoding(936).GetBytes(s);
            Assert(QuoteClient.DecodeGbk(bytes) == s);
        });
        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        return fail == 0 ? 0 : 1;
    }

    static string TEntry(string sym, string code, string name, string price, string pct)
    {
        var f = new string[33];
        for (int i = 0; i < 33; i++) f[i] = "0";
        f[1] = name; f[2] = code; f[3] = price; f[31] = pct; f[32] = pct;
        return "v_" + sym + "=\"" + string.Join("~", f) + "\"";
    }

    static int Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + name + ": " + e.Message);
            return 1;
        }
    }

    static void Assert(bool condition)
    {
        if (!condition) throw new Exception("assert failed");
    }
}
