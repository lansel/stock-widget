using System;
using System.Reflection;
using Microsoft.Win32;

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StockWidget";

    private static string _lastValue;

    // 写入/刷新自启项；args 中的 --config 一并保留，开机后仍用同一份配置
    public static void Ensure(string[] args)
    {
        string exePath = Assembly.GetEntryAssembly().Location;
        _lastValue = "\"" + exePath + "\"" + GetConfigSuffix(args);
        Write(_lastValue);
    }

    // 热切换时复用首次构建的值
    public static void Ensure()
    {
        if (_lastValue == null) Ensure(new string[0]);
        else Write(_lastValue);
    }

    public static void Remove()
    {
        _lastValue = null;
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key != null) key.DeleteValue(ValueName, false);
            }
        }
        catch { }
    }

    private static void Write(string value)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key != null) key.SetValue(ValueName, value);
            }
        }
        catch { }
    }

    private static string GetConfigSuffix(string[] args)
    {
        if (args == null) return "";
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config" && !string.IsNullOrEmpty(args[i + 1]))
                return " --config \"" + args[i + 1] + "\"";
        }
        return "";
    }
}
