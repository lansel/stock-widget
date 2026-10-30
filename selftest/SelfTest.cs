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
            Assert(StockList.ToSymbol("000016") == "sh000016");
            Assert(StockList.ToSymbol("000300") == "sh000300");
            Assert(StockList.ToSymbol("000688") == "sh000688");
            Assert(StockList.ToSymbol("000852") == "sh000852");
            Assert(StockList.ToSymbol("000905") == "sh000905");
            Assert(StockList.ToSymbol("399001") == "sz399001");
            Assert(StockList.ToSymbol("399006") == "sz399006");
        });
        fail += Run("StockList.ToSymbol etf and lof", () => {
            Assert(StockList.ToSymbol("510300") == "sh510300");
            Assert(StockList.ToSymbol("513100") == "sh513100");
            Assert(StockList.ToSymbol("588000") == "sh588000");
            Assert(StockList.ToSymbol("159915") == "sz159915");
            Assert(StockList.ToSymbol("161725") == "sz161725");
        });
        fail += Run("StockList.ToSymbol empty input", () => {
            Assert(StockList.ToSymbol("") == "");
            Assert(StockList.ToSymbol("   ") == "");
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
        fail += Run("StockList.ParseWidgetSettings autostart", () => {
            Assert(StockList.ParseWidgetSettings("600519\n").Autostart);
            Assert(!StockList.ParseWidgetSettings("autostart=false\n").Autostart);
            Assert(StockList.ParseWidgetSettings("autostart=true\nautostart=yes\n").Autostart);
        });
        fail += Run("StockList.ParseWidgetSettings alias", () => {
            var s = StockList.ParseWidgetSettings("600519 茅台\n000001\nsh000300 沪深300\n");
            Assert(s.Codes.Count == 3 && s.Aliases.Count == 3);
            Assert(s.Codes[0] == "600519" && s.Aliases[0] == "茅台");
            Assert(s.Aliases[1] == "");
            Assert(s.Codes[2] == "sh000300" && s.Aliases[2] == "沪深300");
        });
        fail += Run("StockList alias dedupe comment and invalid", () => {
            var s = StockList.ParseWidgetSettings("600519 A\n600519 B # 注\n600519\tC\nabc 名字\n000001\n");
            Assert(s.Codes.Count == 2 && s.Aliases[0] == "A" && s.Aliases[1] == "");
        });
        fail += Run("StockList.ParseWidgetSettings showPrice", () => {
            Assert(!StockList.ParseWidgetSettings("600519\n").ShowPrice);
            Assert(StockList.ParseWidgetSettings("showPrice=true\n600519\n").ShowPrice);
            Assert(!StockList.ParseWidgetSettings("showPrice=yes\n").ShowPrice);
        });
        fail += Run("StockList.LoadSettingsOrDefault", () => {
            var d = StockList.LoadSettingsOrDefault(@"E:\no_such_dir\stocks.txt");
            Assert(d.Autostart && d.Codes.Count == 0);
            string p = NewConfigPath();
            try {
                WriteConfig(p, "autostart=false\n000300\n");
                var s = StockList.LoadSettingsOrDefault(p);
                Assert(!s.Autostart && s.Codes.Count == 1 && s.Codes[0] == "000300");
            } finally { if (File.Exists(p)) File.Delete(p); }
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
        fail += Run("PosConfig.Clamp with size", () => {
            var area = new Rectangle(0, 0, 1920, 1040);
            var size = new Size(190, 60);
            Assert(PosConfig.Clamp(new Point(-50, 10), area, size) == new Point(0, 10));
            Assert(PosConfig.Clamp(new Point(10, -5), area, size) == new Point(10, 0));
            Assert(PosConfig.Clamp(new Point(5000, 5000), area, size) == new Point(1730, 980));
            Assert(PosConfig.Clamp(new Point(1800, 1000), area, size) == new Point(1730, 980));
            Assert(PosConfig.Clamp(new Point(100, 100), area, size) == new Point(100, 100));
            var small = new Rectangle(0, 0, 100, 40);
            Assert(PosConfig.Clamp(new Point(10, 10), small, size) == new Point(0, 0));
        });
        fail += Run("ConfigWatcher missing startup", () => {
            string p = NewConfigPath();
            var w = new ConfigWatcher(p);
            var s = w.Poll(DateTime.UtcNow);
            Assert(s.Changed && !s.Exists && s.Settings.Codes.Count == 0);
        });
        fail += Run("ConfigWatcher code change immediate", () => {
            string p = NewConfigPath();
            try {
                WriteConfig(p, "600519\n000001\n");
                var w = new ConfigWatcher(p);
                var t = DateTime.UtcNow;
                var s1 = w.Poll(t);
                Assert(s1.Changed && s1.Exists && s1.Settings.Codes.Count == 2);
                WriteConfig(p, "600519\n");
                var s2 = w.Poll(t.AddMilliseconds(250));
                Assert(s2.Changed && s2.Settings.Codes.Count == 1);
            } finally { if (File.Exists(p)) File.Delete(p); }
        });
        fail += Run("ConfigWatcher empty debounce", () => {
            string p = NewConfigPath();
            try {
                WriteConfig(p, "600519\n");
                var w = new ConfigWatcher(p);
                var t = DateTime.UtcNow;
                Assert(w.Poll(t).Settings.Codes.Count == 1);
                WriteConfig(p, "");
                var a = w.Poll(t.AddMilliseconds(250));
                Assert(!a.Changed && a.Settings.Codes.Count == 1);
                var b = w.Poll(t.AddMilliseconds(800));
                Assert(b.Changed && b.Exists && b.Settings.Codes.Count == 0);
            } finally { if (File.Exists(p)) File.Delete(p); }
        });
        fail += Run("ConfigWatcher recover during debounce", () => {
            string p = NewConfigPath();
            try {
                WriteConfig(p, "600519\n");
                var w = new ConfigWatcher(p);
                var t = DateTime.UtcNow;
                Assert(w.Poll(t).Settings.Codes.Count == 1);
                WriteConfig(p, "");
                Assert(!w.Poll(t.AddMilliseconds(250)).Changed);
                WriteConfig(p, "000001\n");
                var r = w.Poll(t.AddMilliseconds(300));
                Assert(r.Changed && r.Settings.Codes.Count == 1 && r.Settings.Codes[0] == "000001");
            } finally { if (File.Exists(p)) File.Delete(p); }
        });
        fail += Run("ConfigWatcher missing debounce", () => {
            string p = NewConfigPath();
            try {
                WriteConfig(p, "600519\n");
                var w = new ConfigWatcher(p);
                var t = DateTime.UtcNow;
                Assert(w.Poll(t).Settings.Codes.Count == 1);
                File.Delete(p);
                var a = w.Poll(t.AddMilliseconds(250));
                Assert(!a.Changed && a.Settings.Codes.Count == 1);
                var b = w.Poll(t.AddMilliseconds(800));
                Assert(b.Changed && !b.Exists && b.Settings.Codes.Count == 0);
            } finally { if (File.Exists(p)) File.Delete(p); }
        });
        fail += Run("ConfigWatcher settings only", () => {
            string p = NewConfigPath();
            try {
                WriteConfig(p, "nameColor=#FF0000\n");
                var w = new ConfigWatcher(p);
                var s = w.Poll(DateTime.UtcNow);
                Assert(s.Changed && s.Exists && s.Settings.Codes.Count == 0);
                Assert(s.Settings.NameColor.R == 255 && s.Settings.NameColor.G == 0);
            } finally { if (File.Exists(p)) File.Delete(p); }
        });
        fail += Run("DesktopShowState hide on Win+D", () => {
            var d = DesktopShowState.Decide(DS(true, true, true, ForegroundKind.Desktop, false, false, false, false));
            Assert(d.Command == DesktopCommand.Hide && d.ClearWinDToggle);
        });
        fail += Run("DesktopShowState Win+D while hidden stays hidden", () => {
            var d = DesktopShowState.Decide(DS(true, true, false, ForegroundKind.App, false, false, false, false));
            Assert(d.Command == DesktopCommand.None && d.ClearWinDToggle);
        });
        fail += Run("DesktopShowState hidden app restores", () => {
            var d = DesktopShowState.Decide(DS(true, false, false, ForegroundKind.App, false, false, false, false));
            Assert(d.Command == DesktopCommand.Show);
        });
        fail += Run("DesktopShowState hidden desktop stays", () => {
            var d = DesktopShowState.Decide(DS(true, false, false, ForegroundKind.Desktop, false, false, false, false));
            Assert(d.Command == DesktopCommand.None);
        });
        fail += Run("DesktopShowState leaving desktop drops", () => {
            var d = DesktopShowState.Decide(DS(false, false, true, ForegroundKind.App, false, false, false, true));
            Assert(d.Command == DesktopCommand.DropToBottom && !d.LastDesktopShown);
        });
        fail += Run("DesktopShowState wallpaper click no topmost", () => {
            var d = DesktopShowState.Decide(DS(false, false, true, ForegroundKind.Desktop, true, true, false, false));
            Assert(d.Command == DesktopCommand.None);
        });
        fail += Run("DesktopShowState Win+D topmost", () => {
            var d = DesktopShowState.Decide(DS(false, false, true, ForegroundKind.Desktop, true, false, false, false));
            Assert(d.Command == DesktopCommand.SetTopMost && d.LastDesktopShown);
        });
        fail += Run("DesktopShowState taskbar no action", () => {
            var d = DesktopShowState.Decide(DS(false, false, true, ForegroundKind.Taskbar, true, true, false, false));
            Assert(d.Command == DesktopCommand.None);
        });
        fail += Run("DesktopShowState default mode clears Win+D flag", () => {
            var d = DesktopShowState.Decide(DS(false, true, true, ForegroundKind.App, false, false, false, false));
            Assert(d.Command == DesktopCommand.None && d.ClearWinDToggle);
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
        fail += Run("QuoteParser.Parse malformed numbers", () => {
            string good = TEntry("sh600519", "600519", "A", "1369.04", "1.11");
            string emptyPrice = TEntry("sz000001", "000001", "B", "", "0.50");
            string badPct = TEntry("sz300750", "300750", "C", "200.00", "abc");
            var r = QuoteParser.Parse(good + ";\n" + emptyPrice + ";\n" + badPct + ";");
            Assert(r.Count == 1);
            Assert(r["sh600519"].Pct == 1.11);
        });
        fail += Run("StockList inline comments", () => {
            var s = StockList.ParseWidgetSettings(
                "600519 # x\n000001#y\nhideOnWinD=true # z\nopacity=0.9 # dim\nbgColor=#202024\nnameColor=#F5F5F5 # 白色\nnameColor=#00FF00#紧贴\n");
            Assert(s.Codes.Count == 2 && s.Codes[0] == "600519" && s.Codes[1] == "000001");
            Assert(s.HideOnWinD);
            Assert(Math.Abs(s.Opacity - 0.9) < 0.0001);
            Assert(s.BgColor.R == 0x20 && s.BgColor.G == 0x20 && s.BgColor.B == 0x24);
            Assert(s.NameColor.R == 0x00 && s.NameColor.G == 0xFF && s.NameColor.B == 0x00);
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
        fail += Run("TradingClock holidays 2026", () => {
            Assert(TradingClock.GetStatus(new DateTime(2026, 1, 1, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 1, 2, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 2, 18, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 2, 23, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 4, 6, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 5, 1, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 6, 19, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 9, 25, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 10, 1, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2026, 10, 7, 10, 0, 0)) == MarketStatus.Closed);
        });
        fail += Run("TradingClock holiday edges", () => {
            Assert(TradingClock.GetStatus(new DateTime(2026, 1, 5, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(new DateTime(2026, 2, 24, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(new DateTime(2026, 9, 30, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(new DateTime(2026, 2, 14, 10, 0, 0)) == MarketStatus.Weekend);
            Assert(TradingClock.GetStatus(new DateTime(2026, 2, 28, 10, 0, 0)) == MarketStatus.Weekend);
        });
        fail += Run("TradingClock 2027 degrades gracefully", () => {
            Assert(TradingClock.GetStatus(new DateTime(2027, 1, 1, 10, 0, 0)) == MarketStatus.Open);
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

    static int _writeTick;
    static string NewConfigPath()
    {
        return Path.Combine(Path.GetTempPath(), "sw_cfg_" + Guid.NewGuid().ToString("N") + ".txt");
    }

    static void WriteConfig(string path, string text)
    {
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(++_writeTick));
    }

    static DesktopState DS(bool hide, bool winD, bool visible, ForegroundKind fg,
        bool lastExists, bool lastVisible, bool lastIconic, bool lastShown)
    {
        return new DesktopState {
            HideOnWinD = hide, WinDToggled = winD, WidgetVisible = visible,
            Foreground = fg, LastAppExists = lastExists, LastAppVisible = lastVisible,
            LastAppIconic = lastIconic, LastDesktopShown = lastShown
        };
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
