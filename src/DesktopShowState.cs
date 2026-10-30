using System;

public enum ForegroundKind
{
    App,
    Desktop,
    Taskbar
}

public enum DesktopCommand
{
    None,
    Hide,
    Show,
    SetTopMost,
    DropToBottom
}

public struct DesktopState
{
    public bool HideOnWinD;
    public bool WinDToggled;
    public bool WidgetVisible;
    public ForegroundKind Foreground;
    public bool LastAppExists;
    public bool LastAppVisible;
    public bool LastAppIconic;
    public bool LastDesktopShown;
}

public struct DesktopDecision
{
    public DesktopCommand Command;
    public bool LastDesktopShown;
    public bool ClearWinDToggle;
}

public static class DesktopShowState
{
    public static DesktopDecision Decide(DesktopState state)
    {
        var decision = new DesktopDecision();
        if (state.WinDToggled) decision.ClearWinDToggle = true;

        if (state.HideOnWinD)
        {
            if (state.WinDToggled)
            {
                if (state.WidgetVisible) decision.Command = DesktopCommand.Hide;
                decision.LastDesktopShown = state.LastDesktopShown;
                return decision;
            }

            if (!state.WidgetVisible
                && state.Foreground != ForegroundKind.Desktop
                && state.Foreground != ForegroundKind.Taskbar)
                decision.Command = DesktopCommand.Show;

            decision.LastDesktopShown = state.LastDesktopShown;
            return decision;
        }

        decision.LastDesktopShown = state.LastDesktopShown;

        if (state.Foreground == ForegroundKind.Taskbar)
            return decision;

        if (state.Foreground != ForegroundKind.Desktop)
        {
            if (state.LastDesktopShown)
            {
                decision.Command = DesktopCommand.DropToBottom;
                decision.LastDesktopShown = false;
            }
            return decision;
        }

        if (state.LastDesktopShown) return decision;
        if (state.LastAppExists && state.LastAppVisible && !state.LastAppIconic) return decision;

        decision.Command = DesktopCommand.SetTopMost;
        decision.LastDesktopShown = true;
        return decision;
    }
}
