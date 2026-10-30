using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class WidgetForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public uint length;
        public uint flags;
        public uint showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    private const int SW_SHOWNOACTIVATE = 4;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_D = 0x44;

    private readonly Timer _zTick;
    private readonly string _configPath;
    private LowLevelKeyboardProc _keyboardProc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _winKeyDown;
    private bool _winDToggle;
    private bool _hideOnWinD;

    private readonly Font _rowFont = new Font("Microsoft YaHei UI", 10f);
    private readonly List<Label> _rowNameLabels = new List<Label>();
    private readonly List<Label> _rowPctLabels = new List<Label>();
    private List<string> _codes = new List<string>();
    private Dictionary<string, Quote> _quotes = new Dictionary<string, Quote>();
    private bool _fetchBusy;
    private Timer _quoteTimer;
    private Point _dragStart;
    private bool _dragging;

    private static readonly Color ColorUp = Color.FromArgb(230, 70, 70);
    private static readonly Color ColorDown = Color.FromArgb(50, 190, 120);
    private static readonly Color ColorFlat = Color.Gray;

    private const int RowHeight = 26;
    private const int MarginX = 12;
    private const int MarginY = 10;

    public WidgetForm(string configPath)
    {
        _configPath = configPath;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 36);
        Opacity = 0.92;
        Size = new Size(190, 60);

        Point saved = PosConfig.Load(PosConfig.DefaultPath);
        if (saved == Point.Empty)
        {
            Location = new Point(
                Screen.PrimaryScreen.WorkingArea.Right - Width - 24,
                Screen.PrimaryScreen.WorkingArea.Top + 24);
        }
        else
        {
            Location = PosConfig.Clamp(saved, Screen.PrimaryScreen.WorkingArea);
        }

        MouseDown += OnFormMouseDown;
        MouseMove += OnFormMouseMove;
        MouseUp += OnFormMouseUp;
        Resize += OnFormResize;

        _zTick = new Timer();
        _zTick.Interval = 250;
        _zTick.Tick += OnZTick;
        _zTick.Start();

        ReloadConfigAndRebuild();
        InstallKeyboardHook();
        StartQuoteTimer(TradingClock.GetIntervalMs(TradingClock.GetStatus(DateTime.Now)));
        FetchOnce();
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
            _rowFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private Color _nameColor = Color.WhiteSmoke;

    private void ReloadConfigAndRebuild()
    {
        try
        {
            if (!string.IsNullOrEmpty(_configPath) && File.Exists(_configPath))
            {
                var settings = StockList.ParseWidgetSettings(File.ReadAllText(_configPath));
                _hideOnWinD = settings.HideOnWinD;
                Opacity = settings.Opacity;
                BackColor = settings.BgColor;
                _nameColor = settings.NameColor;
                bool changed = !SameCodes(_codes, settings.Codes);
                _codes = settings.Codes;
                if (changed) RebuildRows();
            }
            else
            {
                _hideOnWinD = false;
                if (_codes.Count > 0)
                {
                    _codes = new List<string>();
                    RebuildRows();
                }
            }
        }
        catch { }
    }

    private static bool SameCodes(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private void RebuildRows()
    {
        foreach (var l in _rowNameLabels) { Controls.Remove(l); l.Dispose(); }
        foreach (var l in _rowPctLabels) { Controls.Remove(l); l.Dispose(); }
        _rowNameLabels.Clear();
        _rowPctLabels.Clear();

        for (int i = 0; i < _codes.Count; i++)
        {
            var name = new Label();
            name.Font = _rowFont;
            name.ForeColor = _nameColor;
            name.AutoSize = true;
            name.Text = _codes[i];
            Controls.Add(name);
            _rowNameLabels.Add(name);

            var pct = new Label();
            pct.Font = _rowFont;
            pct.AutoSize = true;
            pct.Text = "--";
            pct.ForeColor = ColorFlat;
            Controls.Add(pct);
            _rowPctLabels.Add(pct);
        }

        LayoutRows();
    }

    private void LayoutRows()
    {
        int y = MarginY;
        for (int i = 0; i < _rowNameLabels.Count; i++)
        {
            _rowNameLabels[i].Location = new Point(MarginX, y);
            _rowPctLabels[i].Location = new Point(MarginX + 96, y);
            y += RowHeight;
        }

        int h = y + MarginY;
        if (h < 50) h = 50;
        Size = new Size(Width, h);
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
        ReloadConfigAndRebuild();

        if (_winDToggle)
        {
            _winDToggle = false;
            if (_hideOnWinD)
            {
                Hide();
                return;
            }
        }

        if (_hideOnWinD && !Visible)
        {
            if (!IsDesktopForeground())
                Show();
            return;
        }

        if (IsDesktopForeground())
            BringToTopNonTopMost();
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
                    if (ok && raw != null)
                    {
                        var parsed = QuoteParser.Parse(raw);
                        foreach (var kv in parsed) _quotes[kv.Key] = kv.Value;
                    }
                    RenderQuotes();
                    _fetchBusy = false;
                }));
            }
            catch { _fetchBusy = false; }
        });
    }

    private void RenderQuotes()
    {
        for (int i = 0; i < _codes.Count && i < _rowNameLabels.Count; i++)
        {
            string code = _codes[i];
            string symbol = StockList.ToSymbol(code);
            Quote q;
            if (_quotes.TryGetValue(symbol, out q))
            {
                _rowNameLabels[i].Text = q.Name;
                _rowPctLabels[i].Text = QuoteParser.FormatPct(q.Pct);
                _rowPctLabels[i].ForeColor =
                    q.Pct > 0 ? ColorUp :
                    q.Pct < 0 ? ColorDown : ColorFlat;
            }
            else
            {
                _rowNameLabels[i].Text = code;
                _rowPctLabels[i].Text = "无数据";
                _rowPctLabels[i].ForeColor = ColorFlat;
            }
        }
    }

    private void BringToTopNonTopMost()
    {
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetWindowPos(Handle, HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private static bool IsDesktopForeground()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var sb = new System.Text.StringBuilder(256);
        GetClassName(fg, sb, 256);
        string cls = sb.ToString();
        return cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd";
    }

    private void OnFormMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragStart = e.Location;
    }

    private void OnFormMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
    }

    private void OnFormMouseUp(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Location = PosConfig.Clamp(Location, Screen.PrimaryScreen.WorkingArea);
        PosConfig.Save(PosConfig.DefaultPath, Location);
    }

    private void OnFormResize(object sender, EventArgs e)
    {
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        int radius = 10;
        int w = Width, h = Height;
        if (w < radius * 2 || h < radius * 2) { Region = null; return; }
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(w - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(w - radius * 2, h - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, h - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }
    }
}
