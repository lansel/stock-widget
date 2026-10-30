using System;
using System.Reflection;
using Microsoft.Win32;

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StockWidget";

    public static void Ensure()
    {
        try
        {
            string exePath = Assembly.GetEntryAssembly().Location;
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key != null)
                    key.SetValue(ValueName, "\"" + exePath + "\"");
            }
        }
        catch { }
    }
}
