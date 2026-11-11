using System;
using System.Runtime.InteropServices;

public static class IdleClock
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static uint GetIdleMs()
    {
        var info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref info)) return 0;
        return PollPolicy.GetIdleMs(info.dwTime, unchecked((uint)Environment.TickCount));
    }
}
