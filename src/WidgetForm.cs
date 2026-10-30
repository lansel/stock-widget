using System;
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

    private readonly Timer _tick;
    private readonly string _configPath;
    private LowLevelKeyboardProc _keyboardProc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _winKeyDown;
    private bool _winDToggle;
    private bool _hideOnWinD;

    public WidgetForm(string configPath)
    {
        _configPath = configPath;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 36);
        Size = new Size(190, 110);
        Location = new Point(
            Screen.PrimaryScreen.WorkingArea.Right - Width - 24,
            Screen.PrimaryScreen.WorkingArea.Top + 24);

        var rowFont = new Font("Microsoft YaHei UI", 10f);
        var statusFont = new Font("Microsoft YaHei UI", 8f);

        var row1 = new Label();
        row1.Text = "示例股票  +0.00%";
        row1.ForeColor = Color.WhiteSmoke;
        row1.Font = rowFont;
        row1.Location = new Point(12, 12);
        row1.AutoSize = true;

        var row2 = new Label();
        row2.Text = "测试股票  -1.23%";
        row2.ForeColor = Color.WhiteSmoke;
        row2.Font = rowFont;
        row2.Location = new Point(12, 40);
        row2.AutoSize = true;

        var status = new Label();
        status.Text = "测试壳";
        status.ForeColor = Color.FromArgb(180, 180, 180);
        status.Font = statusFont;
        status.Location = new Point(12, 78);
        status.AutoSize = true;

        Controls.Add(row1);
        Controls.Add(row2);
        Controls.Add(status);

        _tick = new Timer();
        _tick.Interval = 250;
        _tick.Tick += OnTick;
        _tick.Start();

        ReloadSettings();
        InstallKeyboardHook();
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
        if (disposing && _hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        base.Dispose(disposing);
    }

    private void ReloadSettings()
    {
        try
        {
            if (!string.IsNullOrEmpty(_configPath) && File.Exists(_configPath))
            {
                var settings = StockList.ParseWidgetSettings(File.ReadAllText(_configPath));
                _hideOnWinD = settings.HideOnWinD;
            }
        }
        catch { }
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

    private void OnTick(object sender, EventArgs e)
    {
        ReloadSettings();

        // Win+D 切换：hideOnWinD=true 时隐藏，false 时走原有的"显示在最前"逻辑
        if (_winDToggle)
        {
            _winDToggle = false;
            if (_hideOnWinD)
            {
                Hide();
                return;
            }
        }

        // hideOnWinD 模式下：已隐藏且前台是任意软件窗口 → 自动恢复
        // 只 Show，不调整 z-order——刚激活的软件自然会在我们上面
        if (_hideOnWinD && !Visible)
        {
            if (!IsDesktopForeground())
                Show();
            return;
        }

        // 原有模式：桌面前台时拉回最前（临时 TOPMOST → NOTOPMOST）
        if (IsDesktopForeground())
            BringToTopNonTopMost();
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
}
