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
        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        return fail == 0 ? 0 : 1;
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
