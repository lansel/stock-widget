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
        fail += Run("StockList.ParseWidgetSettings pauseWhenAway", () => {
            Assert(StockList.ParseWidgetSettings("600519\n").PauseWhenAway);
            Assert(!StockList.ParseWidgetSettings("pauseWhenAway=false\n").PauseWhenAway);
            Assert(StockList.ParseWidgetSettings("pauseWhenAway=true\n").PauseWhenAway);
            Assert(StockList.ParseWidgetSettings("pauseWhenAway=yes\n").PauseWhenAway);
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
        fail += Run("PollPolicy default foreground gating", () => {
            Assert(PollPolicy.ShouldPoll(true, true, false, true, ForegroundKind.Desktop, 0));
            Assert(PollPolicy.ShouldPoll(true, true, false, true, ForegroundKind.Taskbar, 0));
            Assert(!PollPolicy.ShouldPoll(true, true, false, true, ForegroundKind.App, 0));
            Assert(!PollPolicy.ShouldPoll(true, true, false, false, ForegroundKind.Desktop, 0));
        });
        fail += Run("PollPolicy hidden widget", () => {
            Assert(!PollPolicy.ShouldPoll(false, true, false, true, ForegroundKind.Desktop, 0));
            Assert(!PollPolicy.ShouldPoll(false, true, true, true, ForegroundKind.App, 0));
        });
        fail += Run("PollPolicy idle boundary", () => {
            Assert(PollPolicy.ShouldPoll(true, true, false, true, ForegroundKind.Desktop, PollPolicy.IdleThresholdMs - 1));
            Assert(!PollPolicy.ShouldPoll(true, true, false, true, ForegroundKind.Desktop, PollPolicy.IdleThresholdMs));
        });
        fail += Run("PollPolicy hideOnWinD visible over app", () => {
            Assert(PollPolicy.ShouldPoll(true, true, true, true, ForegroundKind.App, 0));
            Assert(!PollPolicy.ShouldPoll(true, true, true, false, ForegroundKind.App, 0));
            Assert(!PollPolicy.ShouldPoll(true, true, true, true, ForegroundKind.App, PollPolicy.IdleThresholdMs));
        });
        fail += Run("PollPolicy legacy mode always polls when visible", () => {
            Assert(PollPolicy.ShouldPoll(true, false, false, true, ForegroundKind.App, 0));
            Assert(PollPolicy.ShouldPoll(true, false, false, false, ForegroundKind.Desktop, 0));
            Assert(PollPolicy.ShouldPoll(true, false, false, true, ForegroundKind.App, PollPolicy.IdleThresholdMs));
            Assert(!PollPolicy.ShouldPoll(false, false, false, true, ForegroundKind.App, 0));
        });
        fail += Run("PollPolicy idle ticks wrap", () => {
            Assert(PollPolicy.GetIdleMs(1000u, 1500u) == 500u);
            Assert(PollPolicy.GetIdleMs(uint.MaxValue - 100u, 100u) == 201u);
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
        fail += Run("TradingClock holidays 2027", () => {
            Assert(TradingClock.GetStatus(new DateTime(2027, 1, 1, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 2, 5, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 2, 8, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 2, 12, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 4, 5, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 4, 30, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 5, 3, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 5, 4, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 6, 9, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 9, 15, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 10, 1, 10, 0, 0)) == MarketStatus.Closed);
            Assert(TradingClock.GetStatus(new DateTime(2027, 10, 7, 10, 0, 0)) == MarketStatus.Closed);
        });
        fail += Run("TradingClock 2027 holiday edges", () => {
            Assert(TradingClock.GetStatus(new DateTime(2027, 1, 4, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(new DateTime(2027, 2, 15, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(new DateTime(2027, 5, 5, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.GetStatus(new DateTime(2027, 10, 8, 10, 0, 0)) == MarketStatus.Open);
            Assert(TradingClock.IsNonTradingDay(new DateTime(2027, 2, 5, 9, 0, 0)));
            Assert(!TradingClock.IsNonTradingDay(new DateTime(2027, 2, 15, 10, 0, 0)));
        });
        fail += Run("TradingClock 2028 degrades gracefully", () => {
            Assert(TradingClock.GetStatus(new DateTime(2028, 1, 3, 10, 0, 0)) == MarketStatus.Open);
        });
        fail += Run("TradingClock.IsNonTradingDay", () => {
            Assert(TradingClock.IsNonTradingDay(new DateTime(2026, 10, 31, 9, 0, 0)));
            Assert(TradingClock.IsNonTradingDay(new DateTime(2026, 11, 1, 10, 0, 0)));
            Assert(TradingClock.IsNonTradingDay(new DateTime(2026, 10, 1, 10, 0, 0)));
            Assert(TradingClock.IsNonTradingDay(new DateTime(2026, 2, 14, 10, 0, 0)));
            Assert(!TradingClock.IsNonTradingDay(new DateTime(2026, 9, 30, 10, 0, 0)));
            Assert(!TradingClock.IsNonTradingDay(new DateTime(2026, 12, 31, 14, 0, 0)));
            Assert(TradingClock.IsNonTradingDay(new DateTime(2027, 1, 1, 10, 0, 0)));
            Assert(!TradingClock.IsNonTradingDay(new DateTime(2028, 1, 3, 10, 0, 0)));
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
        fail += Run("ChartClient.BuildKlineUrl", () => {
            Assert(ChartClient.BuildKlineUrl("sh600519", "week", 120)
                == "https://ifzq.gtimg.cn/appstock/app/fqkline/get?param=sh600519,week,,,120,qfq");
            Assert(ChartClient.BuildKlineUrl("sh000001", "month", 120)
                == "https://ifzq.gtimg.cn/appstock/app/fqkline/get?param=sh000001,month,,,120,qfq");
            Assert(ChartClient.BuildDailyUrl("sz159915", 120)
                == "https://ifzq.gtimg.cn/appstock/app/fqkline/get?param=sz159915,day,,,120,qfq");
        });
        fail += Run("ChartParser.TimeToMinuteIndex", () => {
            Assert(ChartParser.TimeToMinuteIndex("0930") == 0);
            Assert(ChartParser.TimeToMinuteIndex("0931") == 1);
            Assert(ChartParser.TimeToMinuteIndex("1130") == 120);
            Assert(ChartParser.TimeToMinuteIndex("1300") == 120);
            Assert(ChartParser.TimeToMinuteIndex("1301") == 121);
            Assert(ChartParser.TimeToMinuteIndex("1500") == 240);
            Assert(ChartParser.TimeToMinuteIndex("0929") == -1);
            Assert(ChartParser.TimeToMinuteIndex("1131") == -1);
            Assert(ChartParser.TimeToMinuteIndex("1501") == -1);
            Assert(ChartParser.TimeToMinuteIndex("") == -1);
        });
        fail += Run("ChartParser.ParseMinute stock", () => {
            string json = "{\"code\":0,\"data\":{\"sh600519\":{\"data\":{\"data\":[\"0930 1440.06 186 26785115.56\",\"0931 1440.99 1110 159723413.00\",\"0932 1444.51 1389 199953471.00\"],\"date\":\"20261109\"},\"qt\":{\"sh600519\":[\"1\",\"\u8d35\u5dde\u8305\u53f0\",\"600519\",\"1443.58\",\"1453.00\"]}}}}";
            MinuteSeries s = ChartParser.ParseMinute("sh600519", json);
            Assert(s.Points.Count == 3);
            Assert(s.HasAvg);
            Assert(s.Date == "20261109");
            Assert(s.Quote != null && s.Quote.PrevClose == 1453.00);
            Assert(s.Quote.Name == "\u8d35\u5dde\u8305\u53f0");
            Assert(Math.Abs(s.Quote.Price - 1443.58) < 1e-9);
            Assert(s.Points[0].MinuteIndex == 0);
            Assert(s.Points[1].MinuteIndex == 1);
            Assert(Math.Abs(s.Points[0].Avg - 1440.06) < 0.001);
            Assert(Math.Abs(s.Points[1].Avg - 159723413.00 / 111000.0) < 0.001);
        });
        fail += Run("ChartParser.ParseMinute index has no avg", () => {
            string json = "{\"data\":{\"sh000001\":{\"data\":{\"data\":[\"0930 3659.02 4291331 5711010174.80\"],\"date\":\"20261109\"},\"qt\":{\"sh000001\":[\"1\",\"\u4e0a\u8bc1\u6307\u6570\",\"000001\",\"3659.02\",\"3650.00\"]}}}}";
            MinuteSeries s = ChartParser.ParseMinute("sh000001", json);
            Assert(!s.HasAvg);
            Assert(s.Points.Count == 1);
        });
        fail += Run("ChartParser.ParseMinute zero volume and empty", () => {
            string json = "{\"data\":{\"sz000001\":{\"data\":{\"data\":[\"0930 10.00 0 0\"],\"date\":\"20261109\"},\"qt\":{\"sz000001\":[\"1\",\"\u5e73\u5b89\u94f6\u884c\",\"000001\",\"10.00\",\"9.90\"]}}}}";
            MinuteSeries s = ChartParser.ParseMinute("sz000001", json);
            Assert(s.Points.Count == 1 && Math.Abs(s.Points[0].Avg - 10.00) < 1e-9);
            Assert(ChartParser.ParseMinute("sh600519", "").Points.Count == 0);
            Assert(ChartParser.ParseMinute("sh600519", "not json").Points.Count == 0);
        });
        fail += Run("ChartParser.ParseDaily qfqday", () => {
            string json = "{\"code\":0,\"data\":{\"sh600519\":{\"qfqday\":[[\"2026-11-06\",\"1450.000\",\"1453.000\",\"1457.720\",\"1436.000\",\"42848.000\"],[\"2026-11-09\",\"1440.060\",\"1443.580\",\"1449.990\",\"1435.080\",\"16001\"]],\"qt\":{\"sh600519\":[\"1\",\"\u8d35\u5dde\u8305\u53f0\",\"600519\",\"1443.58\",\"1453.00\"]}}}}";
            DailySeries s = ChartParser.ParseDaily("sh600519", json);
            Assert(s.Bars.Count == 2);
            Assert(s.Bars[0].Date == "2026-11-06");
            Assert(Math.Abs(s.Bars[1].Open - 1440.06) < 1e-9);
            Assert(Math.Abs(s.Bars[1].Close - 1443.58) < 1e-9);
            Assert(Math.Abs(s.Bars[1].High - 1449.99) < 1e-9);
            Assert(Math.Abs(s.Bars[1].Low - 1435.08) < 1e-9);
            Assert(Math.Abs(s.Bars[1].Volume - 16001) < 1e-9);
        });
        fail += Run("ChartParser.ParseDaily day fallback and bad rows", () => {
            string json = "{\"data\":{\"sh000001\":{\"day\":[[\"2026-11-06\",\"3612.150\",\"3649.180\",\"3660.490\",\"3610.200\",\"565372836.000\"],[\"bad\"],[\"2026-11-07\",\"\",\"1\",\"1\",\"1\",\"1\"]],\"qt\":{\"sh000001\":[\"1\",\"\u4e0a\u8bc1\u6307\u6570\",\"000001\",\"3649.18\",\"3612.15\"]}}}}";
            DailySeries s = ChartParser.ParseDaily("sh000001", json);
            Assert(s.Bars.Count == 1);
            Assert(Math.Abs(s.Bars[0].Close - 3649.18) < 1e-9);
            Assert(ChartParser.ParseDaily("sh600519", "").Bars.Count == 0);
        });
        fail += Run("ChartParser.ParseKline weekly and monthly", () => {
            string weekJson = "{\"data\":{\"sh600519\":{\"qfqweek\":[[\"2026-10-30\",\"1325.400\",\"1392.050\",\"1393.370\",\"1322.600\",\"221992.000\"],[\"2026-11-06\",\"1410.000\",\"1453.000\",\"1480.600\",\"1395.020\",\"311806.000\"]],\"qt\":{\"sh600519\":[\"1\",\"Moutai\",\"600519\",\"1453.00\",\"1444.99\"]}}}}";
            DailySeries w = ChartParser.ParseKline("sh600519", weekJson, "week");
            Assert(w.Bars.Count == 2);
            Assert(w.Bars[1].Date == "2026-11-06");
            Assert(Math.Abs(w.Bars[1].Close - 1453) < 1e-9);
            Assert(Math.Abs(w.Bars[1].Volume - 311806) < 1e-9);
            string monthJson = "{\"data\":{\"sz159915\":{\"qfqmonth\":[[\"2026-10-30\",\"3.161\",\"2.435\",\"3.188\",\"2.433\",\"638695953.000\"]],\"qt\":{\"sz159915\":[\"51\",\"ETF\",\"159915\",\"2.51\",\"2.48\"]}}}}";
            DailySeries m = ChartParser.ParseKline("sz159915", monthJson, "month");
            Assert(m.Bars.Count == 1 && Math.Abs(m.Bars[0].Low - 2.433) < 1e-9);
        });
        fail += Run("ChartParser.ParseKline index fallback", () => {
            string weekJson = "{\"data\":{\"sh000001\":{\"week\":[[\"2026-11-06\",\"3614.310\",\"3649.180\",\"3701.590\",\"3580.380\",\"2749949803.000\"]],\"qt\":{\"sh000001\":[\"1\",\"Index\",\"000001\",\"3649.18\",\"3612.15\"]}}}}";
            DailySeries w = ChartParser.ParseKline("sh000001", weekJson, "week");
            Assert(w.Bars.Count == 1 && Math.Abs(w.Bars[0].Close - 3649.18) < 1e-9);
            string monthJson = "{\"data\":{\"sh000001\":{\"month\":[[\"2026-10-30\",\"3838.980\",\"3599.020\",\"3864.120\",\"3574.270\",\"7811914734.000\"]],\"qt\":{\"sh000001\":[\"1\",\"Index\",\"000001\",\"3599.02\",\"3612.15\"]}}}}";
            DailySeries m = ChartParser.ParseKline("sh000001", monthJson, "month");
            Assert(m.Bars.Count == 1 && Math.Abs(m.Bars[0].Low - 3574.27) < 1e-9);
        });
        fail += Run("ChartParser.IsIndexSymbol", () => {
            Assert(ChartParser.IsIndexSymbol("sh000001"));
            Assert(ChartParser.IsIndexSymbol("sh000300"));
            Assert(ChartParser.IsIndexSymbol("sz399001"));
            Assert(ChartParser.IsIndexSymbol("bj899050"));
            Assert(!ChartParser.IsIndexSymbol("sh600519"));
            Assert(!ChartParser.IsIndexSymbol("sz000001"));
            Assert(!ChartParser.IsIndexSymbol("sz159915"));
        });
        fail += Run("ChartScale.NiceTicks", () => {
            var t = ChartScale.NiceTicks(1400, 1450, 5);
            Assert(t.Count >= 5);
            Assert(t[0] == 1400 && t[1] == 1410);
            Assert(t[t.Count - 1] == 1450);
            var flat = ChartScale.NiceTicks(5, 5, 5);
            Assert(flat.Count >= 2);
            var idx = ChartScale.NiceTicks(3610.2, 3660.49, 5);
            Assert(idx.Count >= 5 && idx[0] >= 3610.2 && idx[idx.Count - 1] <= 3660.49);
        });
        fail += Run("ChartScale.MovingAverage", () => {
            var bars = new List<DailyBar>();
            for (int i = 1; i <= 6; i++)
                bars.Add(new DailyBar { Date = "2026-01-0" + i, Close = i });
            var ma = ChartScale.MovingAverage(bars, 5);
            Assert(ma.Length == 6);
            Assert(double.IsNaN(ma[0]) && double.IsNaN(ma[3]));
            Assert(Math.Abs(ma[4] - 3) < 1e-9);
            Assert(Math.Abs(ma[5] - 4) < 1e-9);
            var shortMa = ChartScale.MovingAverage(bars.GetRange(0, 2), 5);
            Assert(double.IsNaN(shortMa[0]) && double.IsNaN(shortMa[1]));
        });
        fail += Run("ChartScale window and percent and date", () => {
            Assert(ChartScale.VisibleStart(120, 55) == 65);
            Assert(ChartScale.VisibleStart(10, 55) == 0);
            Assert(ChartScale.VisibleStart(0, 55) == 0);
            Assert(Math.Abs(ChartScale.PercentFromPrev(11, 10) - 10) < 1e-9);
            Assert(Math.Abs(ChartScale.PercentFromPrev(9, 10) + 10) < 1e-9);
            Assert(ChartScale.PercentFromPrev(9, 0) == 0);
            Assert(ChartScale.FormatShortDate("2026-11-09") == "11-09");
        });
        fail += Run("ChartScale week and month dates", () => {
            Assert(ChartScale.FormatWeek("2026-11-06") == "26-11-06");
            Assert(ChartScale.FormatMonth("2026-10-30") == "2026-10");
            Assert(ChartScale.FormatWeek("") == "");
            Assert(ChartScale.FormatMonth("2026-10") == "2026-10");
        });
        fail += Run("ChartScale.IntradayMa dynamic", () => {
            var bars = new List<DailyBar>();
            bars.Add(new DailyBar { Date = "2026-11-03", Close = 10 });
            bars.Add(new DailyBar { Date = "2026-11-04", Close = 11 });
            bars.Add(new DailyBar { Date = "2026-11-05", Close = 12 });
            bars.Add(new DailyBar { Date = "2026-11-06", Close = 13 });
            bars.Add(new DailyBar { Date = "2026-11-09", Close = 14 });
            bars.Add(new DailyBar { Date = "2026-11-10", Close = 15 });
            double ma3 = ChartScale.IntradayMa(bars, "20261110", 16, 3);
            Assert(Math.Abs(ma3 - 43.0 / 3.0) < 1e-9);
            double ma5 = ChartScale.IntradayMa(bars, "20261110", 16, 5);
            Assert(Math.Abs(ma5 - 13.2) < 1e-9);
            Assert(double.IsNaN(ChartScale.IntradayMa(bars, "20261110", 16, 10)));
            double next = ChartScale.IntradayMa(bars, "20261111", 16, 3);
            Assert(Math.Abs(next - 15.0) < 1e-9);
            Assert(double.IsNaN(ChartScale.IntradayMa(bars, "20261110", 0, 3)));
            Assert(double.IsNaN(ChartScale.IntradayMa(null, "20261110", 16, 3)));
        });
        fail += Run("ChartScale.MaxVolume visible window", () => {
            var bars = new List<DailyBar>();
            bars.Add(new DailyBar { Volume = 100 });
            bars.Add(new DailyBar { Volume = 300 });
            bars.Add(new DailyBar { Volume = 200 });
            Assert(Math.Abs(ChartScale.MaxVolume(bars, 0) - 300) < 1e-9);
            Assert(Math.Abs(ChartScale.MaxVolume(bars, 1) - 300) < 1e-9);
            Assert(Math.Abs(ChartScale.MaxVolume(bars, 2) - 200) < 1e-9);
            Assert(ChartScale.MaxVolume(bars, 3) == 0);
            Assert(ChartScale.MaxVolume(null, 0) == 0);
            Assert(ChartScale.MaxVolume(new List<DailyBar>(), 0) == 0);
        });
        fail += Run("ChartScale.FormatVolume units", () => {
            Assert(ChartScale.FormatVolume(0) == "0");
            Assert(ChartScale.FormatVolume(9999) == "9999");
            Assert(ChartScale.FormatVolume(10000) == "1.0万");
            Assert(ChartScale.FormatVolume(1234567) == "123.5万");
            Assert(ChartScale.FormatVolume(10000000) == "1000万");
            Assert(ChartScale.FormatVolume(123456789) == "1.23亿");
            Assert(ChartScale.FormatVolume(double.NaN) == "0");
        });
        fail += Run("ChartScale.MinuteVolumes cumulative diff", () => {
            var points = new List<MinutePoint>();
            points.Add(new MinutePoint { Time = "0930", MinuteIndex = 0, Price = 10.0, CumVolume = 100 });
            points.Add(new MinutePoint { Time = "0931", MinuteIndex = 1, Price = 10.1, CumVolume = 350 });
            points.Add(new MinutePoint { Time = "0932", MinuteIndex = 2, Price = 10.0, CumVolume = 300 });
            points.Add(new MinutePoint { Time = "1130", MinuteIndex = 120, Price = 10.2, CumVolume = 1000 });
            points.Add(new MinutePoint { Time = "1300", MinuteIndex = 120, Price = 10.3, CumVolume = 1600 });
            var v = ChartScale.MinuteVolumes(points);
            Assert(v.Length == 241);
            Assert(Math.Abs(v[0] - 100) < 1e-9);
            Assert(Math.Abs(v[1] - 250) < 1e-9);
            Assert(Math.Abs(v[2]) < 1e-9);
            Assert(Math.Abs(v[120] - 1250) < 1e-9);
            Assert(Math.Abs(v[3]) < 1e-9);
            var empty = ChartScale.MinuteVolumes(null);
            Assert(empty.Length == 241 && empty[0] == 0);
        });
        fail += Run("ChartScale limit ratio by board", () => {
            Assert(Math.Abs(ChartScale.LimitRatio("sh600519", "\u8d35\u5dde\u8305\u53f0") - 0.10) < 1e-9);
            Assert(Math.Abs(ChartScale.LimitRatio("sz000001", "\u5e73\u5b89\u94f6\u884c") - 0.10) < 1e-9);
            Assert(Math.Abs(ChartScale.LimitRatio("sz300750", "\u5b81\u5fb7\u65f6\u4ee3") - 0.20) < 1e-9);
            Assert(Math.Abs(ChartScale.LimitRatio("sh688981", "\u4e2d\u82af\u56fd\u9645") - 0.20) < 1e-9);
            Assert(Math.Abs(ChartScale.LimitRatio("bj430047", "\u8bfa\u601d\u5170\u5fb7") - 0.30) < 1e-9);
            Assert(Math.Abs(ChartScale.LimitRatio("sh900901", "\u4e91\u8d5bB\u80a1") - 0.10) < 1e-9);
            Assert(Math.Abs(ChartScale.LimitRatio("sh600519", "*ST\u67d0\u67d0") - 0.05) < 1e-9);
            Assert(ChartScale.LimitRatio("sh000001", "\u4e0a\u8bc1\u6307\u6570") == 0);
            Assert(ChartScale.LimitRatio("sz399001", "\u6df1\u8bc1\u6210\u6307") == 0);
            Assert(ChartScale.LimitRatio("sh510300", "\u6caa\u6df1300ETF") == 0);
            Assert(ChartScale.LimitRatio("sz159915", "\u521b\u4e1a\u677fETF") == 0);
        });
        fail += Run("ChartScale limit up detection", () => {
            var prev = new DailyBar { Date = "2026-11-06", Open = 9.5, Close = 10, High = 10.1, Low = 9.4, Volume = 1 };
            var bar = new DailyBar { Date = "2026-11-09", Open = 10.2, Close = 11, High = 11, Low = 10.1, Volume = 1 };
            Assert(ChartScale.IsLimitUp(bar, prev, 0.10));
            Assert(!ChartScale.IsLimitUp(bar, prev, 0.20));
            Assert(ChartScale.IsLimitUp(new DailyBar { Open = 11, Close = 11, High = 11, Low = 11 }, prev, 0.10));
            Assert(ChartScale.IsLimitUp(new DailyBar { Open = 10.2, Close = 10.99, High = 10.99, Low = 10 }, prev, 0.10));
            Assert(!ChartScale.IsLimitUp(new DailyBar { Open = 10.2, Close = 10.95, High = 10.95, Low = 10 }, prev, 0.10));
            Assert(!ChartScale.IsLimitUp(new DailyBar { Open = 10.2, Close = 10.99, High = 11.00, Low = 10 }, prev, 0.10));
            Assert(!ChartScale.IsLimitUp(bar, null, 0.10));
            Assert(!ChartScale.IsLimitUp(bar, prev, 0));
            Assert(!ChartScale.IsLimitUp(new DailyBar { Open = 10, Close = 10.5, High = 10.5, Low = 10 },
                new DailyBar { Open = 10, Close = 0, High = 10, Low = 10 }, 0.05));
            Assert(ChartScale.IsLimitUp(new DailyBar { Open = 10, Close = 12, High = 12, Low = 10 }, prev, 0.20));
            Assert(ChartScale.IsLimitUp(new DailyBar { Open = 10, Close = 13, High = 13, Low = 10 }, prev, 0.30));
            Assert(ChartScale.IsLimitUp(new DailyBar { Open = 10, Close = 10.5, High = 10.5, Low = 10 }, prev, 0.05));
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
