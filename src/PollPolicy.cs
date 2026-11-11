using System;

public static class PollPolicy
{
    public const uint IdleThresholdMs = 600000;

    public static bool ShouldPoll(bool widgetVisible, bool pauseWhenAway, bool hideOnWinD,
        bool hasForeground, ForegroundKind foreground, uint idleMs)
    {
        if (!widgetVisible) return false;
        if (!pauseWhenAway) return true;
        if (!hasForeground) return false;
        if (idleMs >= IdleThresholdMs) return false;
        if (hideOnWinD) return true;
        return foreground == ForegroundKind.Desktop || foreground == ForegroundKind.Taskbar;
    }

    public static uint GetIdleMs(uint lastInputTick, uint nowTick)
    {
        return unchecked(nowTick - lastInputTick);
    }
}
