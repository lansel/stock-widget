using System;

static class SelfTest
{
    static int Main()
    {
        int fail = 0;
        fail += Run("sanity", () => Assert(1 + 1 == 2));
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
