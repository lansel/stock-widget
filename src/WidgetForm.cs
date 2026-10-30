using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class WidgetForm : Form
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_D = 0x44;

    private readonly Timer _zTick;
    private readonly string _configPath;
    private readonly UiMetrics _metrics;
    private readonly RowView _rows;
    private readonly ContextMenuStrip _menu;
    private readonly ConfigWatcher _config;
    private LowLevelKeyboardProc _keyboardProc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _winKeyDown;
    private bool _winDToggle;
    private bool _hideOnWinD;
    private bool _lastAutostart = true;
    private bool _lastDesktopShown;
    private IntPtr _lastAppFg = IntPtr.Zero;

    private List<string> _codes = new List<string>();
    private Dictionary<string, Quote> _quotes = new Dictionary<string, Quote>();
    private bool _fetchBusy;
    private bool _stale;
    private Timer _quoteTimer;
    private Point _dragStartScreen;
    private Point _formStart;
    private bool _dragging;

    public WidgetForm(string configPath)
    {
        _configPath = configPath;
        _metrics = UiMetrics.FromSystemDpi();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(32, 32, 36);
        Opacity = 0.92;
        Size = new Size(_metrics.Width, _metrics.MinHeight);

        _menu = new ContextMenuStrip();
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += OnExitClick;
        _menu.Items.Add(exitItem);
        ContextMenuStrip = _menu;

        _rows = new RowView(this, _metrics, ConfigureRowTarget);

        MouseDown += OnDragMouseDown;
        MouseMove += OnDragMouseMove;
        MouseUp += OnDragMouseUp;
        Resize += OnFormResize;
        FormClosing += OnFormClosing;

        _zTick = new Timer();
        _zTick.Interval = 250;
        _zTick.Tick += OnZTick;
        _zTick.Start();

        _quoteTimer = new Timer();
        _quoteTimer.Tick += OnQuoteTick;

        _config = new ConfigWatcher(_configPath);
        ApplySnapshot(_config.Poll(DateTime.UtcNow), true);
        ApplySavedPosition();
        UpdateKeyboardHook();
        StartQuoteTimer(TradingClock.GetIntervalMs(TradingClock.GetStatus(DateTime.Now)));
        Shown += OnFormShown;
        ApplyRoundedRegion();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            if (_quoteTimer != null) _quoteTimer.Dispose();
            if (_zTick != null) _zTick.Dispose();
            if (_rows != null) _rows.Dispose();
            if (_menu != null)
            {
                ContextMenuStrip = null;
                _menu.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    private void ApplySnapshot(ConfigSnapshot snapshot, bool initial)
    {
        if (snapshot == null) return;
        if (!initial && !snapshot.Changed) return;

        WidgetSettings settings = snapshot.Settings;
        if (settings == null) settings = new WidgetSettings();

        bool oldHide = _hideOnWinD;
        _hideOnWinD = settings.HideOnWinD;
        Opacity = settings.Opacity;
        BackColor = settings.BgColor;
        _rows.SetNameColor(settings.NameColor);

        bool rebuilt = _rows.SetCodes(settings.Codes, settings.Aliases, settings.ShowPrice, snapshot.Exists);
        if (rebuilt)
        {
            _codes = new List<string>(settings.Codes);
            EnsureOnScreen();
            if (!initial && _codes.Count > 0 && Visible) FetchOnce();
        }

        if (!initial && oldHide != _hideOnWinD)
            ApplyHideModeChange(_hideOnWinD);

        if (settings.Autostart != _lastAutostart)
        {
            _lastAutostart = settings.Autostart;
            if (!initial)
            {
                if (settings.Autostart) Autostart.Ensure();
                else Autostart.Remove();
            }
        }

        _rows.SetStale(_stale && _codes.Count > 0);
    }

    private void ApplyHideModeChange(bool hide)
    {
        ResetDesktopState();
        SetWindowPos(Handle, HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        UpdateKeyboardHook();
        if (!hide && !Visible) ShowWidget();
    }

    private void ResetDesktopState()
    {
        _winKeyDown = false;
        _winDToggle = false;
        _lastDesktopShown = false;
        _lastAppFg = IntPtr.Zero;
    }

    private void UpdateKeyboardHook()
    {
        if (_hideOnWinD)
        {
            if (_hookId == IntPtr.Zero) InstallKeyboardHook();
        }
        else if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _winKeyDown = false;
            _winDToggle = false;
        }
    }

    private void ShowWidget()
    {
        if (Visible) return;
        Show();
        _quoteTimer.Start();
        FetchOnce();
    }

    private void HideWidget()
    {
        if (!Visible) return;
        Hide();
        _quoteTimer.Stop();
    }

    private void OnFormShown(object sender, EventArgs e)
    {
        FetchOnce();
    }

    private void ConfigureRowTarget(Control c)
    {
        c.MouseDown += OnDragMouseDown;
        c.MouseMove += OnDragMouseMove;
        c.MouseUp += OnDragMouseUp;
        c.ContextMenuStrip = _menu;
    }

    private void InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookProc;
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            bool isDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;

            if (isDown)
            {
                if (kb.vkCode == VK_LWIN || kb.vkCode == VK_RWIN)
                    _winKeyDown = true;
                else if (kb.vkCode == VK_D && _winKeyDown)
                    _winDToggle = true;
            }
            else if (kb.vkCode == VK_LWIN || kb.vkCode == VK_RWIN)
            {
                _winKeyDown = false;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void OnZTick(object sender, EventArgs e)
    {
        ConfigSnapshot snapshot = _config.Poll(DateTime.UtcNow);
        if (snapshot.Changed) ApplySnapshot(snapshot, false);

        IntPtr fg = GetForegroundWindow();
        ForegroundKind kind = GetForegroundKind(fg);
        bool lastAppExists = _lastAppFg != IntPtr.Zero && IsWindow(_lastAppFg);
        bool lastAppVisible = lastAppExists && IsWindowVisible(_lastAppFg);
        bool lastAppIconic = lastAppExists && IsIconic(_lastAppFg);

        var state = new DesktopState();
        state.HideOnWinD = _hideOnWinD;
        state.WinDToggled = _winDToggle;
        state.WidgetVisible = Visible;
        state.Foreground = kind;
        state.LastAppExists = lastAppExists;
        state.LastAppVisible = lastAppVisible;
        state.LastAppIconic = lastAppIconic;
        state.LastDesktopShown = _lastDesktopShown;

        DesktopDecision decision = DesktopShowState.Decide(state);
        if (decision.ClearWinDToggle) _winDToggle = false;
        _lastDesktopShown = decision.LastDesktopShown;
        if (kind == ForegroundKind.App && fg != IntPtr.Zero) _lastAppFg = fg;

        switch (decision.Command)
        {
            case DesktopCommand.Hide:
                HideWidget();
                break;
            case DesktopCommand.Show:
                ShowWidget();
                break;
            case DesktopCommand.SetTopMost:
                if (!Visible) ShowWidget();
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                break;
            case DesktopCommand.DropToBottom:
                SetWindowPos(Handle, HWND_NOTOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                break;
        }
    }

    private void StartQuoteTimer(int intervalMs)
    {
        if (_quoteTimer == null)
        {
            _quoteTimer = new Timer();
            _quoteTimer.Tick += OnQuoteTick;
        }
        _quoteTimer.Interval = intervalMs;
        _quoteTimer.Start();
    }

    private void OnQuoteTick(object sender, EventArgs e)
    {
        if (!Visible) return;

        var status = TradingClock.GetStatus(DateTime.Now);
        int want = TradingClock.GetIntervalMs(status);
        if (_quoteTimer.Interval != want)
            _quoteTimer.Interval = want;

        FetchOnce();
    }

    private void FetchOnce()
    {
        if (_fetchBusy || _codes.Count == 0)
        {
            return;
        }
        _fetchBusy = true;
        var codes = new List<string>(_codes);
        System.Threading.Tasks.Task.Factory.StartNew(() =>
        {
            string raw = null;
            bool ok = false;
            try
            {
                var symbols = new string[codes.Count];
                for (int i = 0; i < codes.Count; i++)
                    symbols[i] = StockList.ToSymbol(codes[i]);
                raw = QuoteClient.FetchRaw(symbols);
                ok = true;
            }
            catch { }

            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (ok && raw != null)
                        {
                            var parsed = QuoteParser.Parse(raw);
                            bool any = false;
                            for (int i = 0; i < _codes.Count; i++)
                            {
                                if (parsed.ContainsKey(StockList.ToSymbol(_codes[i])))
                                {
                                    any = true;
                                    break;
                                }
                            }
                            foreach (var kv in parsed) _quotes[kv.Key] = kv.Value;
                            _stale = !any;
                        }
                        else
                        {
                            _stale = true;
                        }
                        _rows.SetStale(_stale && _codes.Count > 0);
                        _rows.Render(_quotes);
                    }
                    catch
                    {
                        _stale = true;
                        _rows.SetStale(_stale && _codes.Count > 0);
                    }
                    finally
                    {
                        _fetchBusy = false;
                    }
                }));
            }
            catch { _fetchBusy = false; }
        });
    }

    private static string GetClassNameOf(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return "";
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hWnd, sb, 256);
        return sb.ToString();
    }

    private static ForegroundKind GetForegroundKind(IntPtr hWnd)
    {
        string cls = GetClassNameOf(hWnd);
        if (cls == "Progman" || cls == "WorkerW") return ForegroundKind.Desktop;
        if (cls == "Shell_TrayWnd") return ForegroundKind.Taskbar;
        return ForegroundKind.App;
    }

    private void OnDragMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        Control c = sender as Control;
        if (c == null) return;
        _dragging = true;
        _dragStartScreen = c.PointToScreen(e.Location);
        _formStart = Location;
        c.Capture = true;
    }

    private void OnDragMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Control c = sender as Control;
        if (c == null) return;
        Point screen = c.PointToScreen(e.Location);
        Location = new Point(
            _formStart.X + screen.X - _dragStartScreen.X,
            _formStart.Y + screen.Y - _dragStartScreen.Y);
    }

    private void OnDragMouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_dragging) return;
        _dragging = false;
        Control c = sender as Control;
        if (c != null) c.Capture = false;
        Rectangle area = GetCurrentScreenArea();
        Location = PosConfig.Clamp(Location, area, Size);
        PosConfig.Save(PosConfig.DefaultPath, Location);
    }

    private Rectangle GetCurrentScreenArea()
    {
        Point center = new Point(Location.X + Width / 2, Location.Y + Height / 2);
        return Screen.FromPoint(center).WorkingArea;
    }

    private void EnsureOnScreen()
    {
        Rectangle area = GetCurrentScreenArea();
        Point clamped = PosConfig.Clamp(Location, area, Size);
        if (clamped != Location) Location = clamped;
    }

    private void ApplySavedPosition()
    {
        Point saved = PosConfig.Load(PosConfig.DefaultPath);
        if (saved == Point.Empty)
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Right - Width - _metrics.DefaultMargin,
                area.Top + _metrics.DefaultMargin);
            Location = PosConfig.Clamp(Location, area, Size);
        }
        else
        {
            Point center = new Point(saved.X + Width / 2, saved.Y + Height / 2);
            Rectangle area = Screen.FromPoint(center).WorkingArea;
            Location = PosConfig.Clamp(saved, area, Size);
        }
    }

    private void OnExitClick(object sender, EventArgs e)
    {
        Close();
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        try
        {
            Rectangle area = GetCurrentScreenArea();
            Location = PosConfig.Clamp(Location, area, Size);
            PosConfig.Save(PosConfig.DefaultPath, Location);
        }
        catch { }
    }

    private void OnFormResize(object sender, EventArgs e)
    {
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        int radius = _metrics.Radius;
        int w = Width, h = Height;
        if (w < radius * 2 || h < radius * 2) { ReplaceRegion(null); return; }
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(w - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(w - radius * 2, h - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, h - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            ReplaceRegion(new Region(path));
        }
    }

    // Control.Region 的 setter 不释放旧 Region，换新前手动释放，避免 GDI 句柄泄漏
    private void ReplaceRegion(Region region)
    {
        Region old = Region;
        Region = region;
        if (old != null) old.Dispose();
    }
}
