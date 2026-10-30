using System;
using System.Collections.Generic;

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
        fail += Run("StockList.GetConfigPath on desktop", () => {
            Assert(StockList.GetConfigPath().EndsWith("stocks.txt"));
        });
        fail += Run("QuoteParser.Parse multi entry", () => {
            string text = TEntry("sh600519", "600519", "\u8d35\u5dde\u8305\u53f0", "1369.04", "1.11")
                + ";\n" + TEntry("sz000001", "000001", "\u5e73\u5b89\u94f6\u884c", "11.47", "-0.61")
                + ";\nv_pv_none_match=\"1\";";
            var r = QuoteParser.Parse(text);
            Assert(r.Count == 2);
            Assert(r["600519"].Name == "\u8d35\u5dde\u8305\u53f0");
            Assert(r["600519"].Price == 1369.04 && r["600519"].Pct == 1.11);
            Assert(r["000001"].Pct == -0.61);
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
