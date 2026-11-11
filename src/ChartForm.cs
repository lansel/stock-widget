using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

public sealed class ChartForm : Form
{
    private static readonly Color ColorUp = Color.FromArgb(230, 70, 70);
    private static readonly Color ColorDown = Color.FromArgb(50, 190, 120);
    private static readonly Color ColorMuted = Color.FromArgb(120, 120, 130);
    private static readonly Color ColorSeparator = Color.FromArgb(58, 58, 64);
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly float _scale;
    private readonly int _headerHeight;
    private readonly ChartCanvas _canvas;
    private readonly Label _title;
    private readonly Label _price;
    private readonly Label _date;
    private readonly Label _btnMinute;
    private readonly Label _btnDaily;
    private readonly Label _btnWeek;
    private readonly Label _btnMonth;
    private readonly Label _close;
    private readonly Timer _timer;
    private readonly Font _titleFont;
    private readonly Font _smallFont;
    private readonly Font _buttonFont;
    private readonly Font _buttonActiveFont;
    private readonly Font _closeFont;
    private readonly Rectangle _widgetBounds;

    private string _code = "";
    private string _alias = "";
    private string _symbol = "";
    private ChartMode _mode = ChartMode.Minute;
    private MinuteSeries _minute;
    private readonly Dictionary<ChartMode, DailySeries> _klines =
        new Dictionary<ChartMode, DailySeries>();
    private bool _busy;
    private int _requestId;
    private bool _minuteReferenceBusy;
    private int _minuteReferenceId;
    private string _referenceDate = "";
    private bool _referenceAttached;
    private DateTime _minuteReferenceRetryAt = DateTime.MinValue;
    private bool _closed;
    private bool _dragging;
    private Point _dragCursor;
    private Point _dragForm;
    private Color _bgColor = Color.FromArgb(32, 32, 36);
    private Color _nameColor = Color.WhiteSmoke;

    public ChartForm(UiMetrics metrics, Rectangle widgetBounds)
    {
        _scale = metrics.Scale;
        _widgetBounds = widgetBounds;
        _headerHeight = Round(36 * _scale);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = _bgColor;
        DoubleBuffered = true;
        KeyPreview = true;
        Size = new Size(Round(680 * _scale), Round(420 * _scale));

        _titleFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        _smallFont = new Font("Microsoft YaHei UI", 8f);
        _buttonFont = new Font("Microsoft YaHei UI", 9f);
        _buttonActiveFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        _closeFont = new Font("Microsoft YaHei UI", 12f);

        _canvas = new ChartCanvas();
        _canvas.UiScale = _scale;
        _canvas.Mode = ChartMode.Minute;
        _canvas.Location = new Point(0, _headerHeight);
        _canvas.Size = new Size(ClientSize.Width, ClientSize.Height - _headerHeight);
        Controls.Add(_canvas);

        _title = CreateHeaderLabel(_titleFont, _nameColor, ContentAlignment.MiddleLeft);
        _price = CreateHeaderLabel(_titleFont, _nameColor, ContentAlignment.MiddleLeft);
        _date = CreateHeaderLabel(_smallFont, ColorMuted, ContentAlignment.MiddleLeft);
        _btnMinute = CreateButton("分时", _buttonFont);
        _btnDaily = CreateButton("日K", _buttonFont);
        _btnWeek = CreateButton("周K", _buttonFont);
        _btnMonth = CreateButton("月K", _buttonFont);
        _close = CreateButton("×", _closeFont);
        LayoutHeader();

        _btnMinute.Click += delegate { SwitchMode(ChartMode.Minute); };
        _btnDaily.Click += delegate { SwitchMode(ChartMode.Daily); };
        _btnWeek.Click += delegate { SwitchMode(ChartMode.Weekly); };
        _btnMonth.Click += delegate { SwitchMode(ChartMode.Monthly); };
        _close.Click += delegate { Close(); };

        AttachDrag(_title);
        AttachDrag(_price);
        AttachDrag(_date);
        MouseDown += OnHeaderMouseDown;
        MouseMove += OnHeaderMouseMove;
        MouseUp += OnHeaderMouseUp;

        KeyDown += OnKeyDown;
        Resize += OnFormResize;
        FormClosing += OnFormClosing;
        Shown += OnFormShown;

        _timer = new Timer();
        _timer.Interval = 60000;
        _timer.Tick += OnTimerTick;

        LoadPosition();
        ApplyRoundedRegion();
        UpdateToggleStyles();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void SetTarget(string code, string alias, string symbol)
    {
        _code = code != null ? code : "";
        _alias = alias != null ? alias : "";
        _symbol = symbol != null ? symbol : "";
        _minute = null;
        _klines.Clear();
        _referenceDate = "";
        _referenceAttached = false;
        _minuteReferenceBusy = false;
        _minuteReferenceRetryAt = DateTime.MinValue;
        _requestId++;
        _minuteReferenceId++;
        _busy = false;
        _canvas.Mode = _mode;
        _canvas.Clear();
        UpdateHeader();
        if (Visible)
        {
            StartFetch(true);
            UpdateTimer();
            if (_mode == ChartMode.Minute) EnsureMinuteReference();
        }
    }

    public void ApplyTheme(Color background, Color nameColor)
    {
        _bgColor = background;
        _nameColor = nameColor;
        BackColor = background;
        _title.ForeColor = nameColor;
        _date.ForeColor = ColorMuted;
        _canvas.ApplyTheme(background, nameColor);
        UpdateToggleStyles();
        UpdateHeader();
        Invalidate();
    }

    private Label CreateHeaderLabel(Font font, Color color, ContentAlignment align)
    {
        var label = new Label();
        label.Font = font;
        label.ForeColor = color;
        label.BackColor = Color.Transparent;
        label.AutoSize = false;
        label.AutoEllipsis = true;
        label.UseMnemonic = false;
        label.TextAlign = align;
        Controls.Add(label);
        return label;
    }

    private Label CreateButton(string text, Font font)
    {
        Label label = CreateHeaderLabel(font, ColorMuted, ContentAlignment.MiddleCenter);
        label.Text = text;
        label.Cursor = Cursors.Hand;
        return label;
    }

    private void LayoutHeader()
    {
        int h = _headerHeight;
        int pad = Round(12 * _scale);
        int buttonWidth = Round(46 * _scale);
        int closeWidth = Round(40 * _scale);
        int closeX = ClientSize.Width - closeWidth;
        int monthX = closeX - buttonWidth;
        int weekX = monthX - buttonWidth;
        int dailyX = weekX - buttonWidth;
        int minuteX = dailyX - buttonWidth;
        int priceWidth = Round(170 * _scale);
        int dateWidth = Round(64 * _scale);
        int titleWidth = minuteX - pad - priceWidth - dateWidth - pad;
        if (titleWidth < Round(60 * _scale)) titleWidth = Round(60 * _scale);

        _title.Bounds = new Rectangle(pad, 0, titleWidth, h);
        _price.Bounds = new Rectangle(pad + titleWidth, 0, priceWidth, h);
        _date.Bounds = new Rectangle(pad + titleWidth + priceWidth, 0, dateWidth, h);
        _btnMinute.Bounds = new Rectangle(minuteX, 0, buttonWidth, h);
        _btnDaily.Bounds = new Rectangle(dailyX, 0, buttonWidth, h);
        _btnWeek.Bounds = new Rectangle(weekX, 0, buttonWidth, h);
        _btnMonth.Bounds = new Rectangle(monthX, 0, buttonWidth, h);
        _close.Bounds = new Rectangle(closeX, 0, closeWidth, h);
    }

    private void AttachDrag(Control c)
    {
        c.MouseDown += OnHeaderMouseDown;
        c.MouseMove += OnHeaderMouseMove;
        c.MouseUp += OnHeaderMouseUp;
    }

    private void OnHeaderMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragCursor = Cursor.Position;
        _dragForm = Location;
        Control c = sender as Control;
        if (c != null) c.Capture = true;
    }

    private void OnHeaderMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Point cursor = Cursor.Position;
        Location = new Point(
            _dragForm.X + cursor.X - _dragCursor.X,
            _dragForm.Y + cursor.Y - _dragCursor.Y);
    }

    private void OnHeaderMouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_dragging) return;
        _dragging = false;
        Control c = sender as Control;
        if (c != null) c.Capture = false;
        SavePosition();
    }

    private void SwitchMode(ChartMode mode)
    {
        if (_mode == mode)
        {
            UpdateToggleStyles();
            return;
        }
        _mode = mode;
        _canvas.Mode = mode;
        UpdateToggleStyles();
        if (mode == ChartMode.Minute)
        {
            if (_minute != null) _canvas.SetMinute(_minute);
            else _canvas.SetState(true, null);
        }
        else
        {
            DailySeries series;
            if (_klines.TryGetValue(mode, out series)) _canvas.SetDaily(series);
            else _canvas.SetState(true, null);
        }
        UpdateHeader();
        _requestId++;
        _busy = false;
        StartFetch(true);
        UpdateTimer();
        if (mode == ChartMode.Minute) EnsureMinuteReference();
    }

    private void OnFormShown(object sender, EventArgs e)
    {
        StartFetch(true);
        UpdateTimer();
        if (_mode == ChartMode.Minute) EnsureMinuteReference();
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        if (!AutoRefresh)
        {
            _timer.Stop();
            return;
        }
        int want = WantedInterval();
        if (_timer.Interval != want) _timer.Interval = want;
        if (_mode == ChartMode.Minute) EnsureMinuteReference();
        StartFetch(false);
    }

    private bool AutoRefresh
    {
        get { return _mode == ChartMode.Minute || _mode == ChartMode.Daily; }
    }

    private void UpdateTimer()
    {
        if (!AutoRefresh)
        {
            _timer.Stop();
            return;
        }
        _timer.Interval = WantedInterval();
        if (!_timer.Enabled) _timer.Start();
    }

    private int WantedInterval()
    {
        if (_mode == ChartMode.Minute)
            return TradingClock.GetStatus(DateTime.Now) == MarketStatus.Open ? 15000 : 60000;
        return 60000;
    }

    private void StartFetch(bool force)
    {
        if (_closed || _symbol.Length == 0) return;
        if (!force && HasModeData(_mode) && TradingClock.IsNonTradingDay(DateTime.Now)) return;
        if (!force && IdleClock.GetIdleMs() >= PollPolicy.IdleThresholdMs) return;
        if (_busy) return;

        _busy = true;
        int id = ++_requestId;
        ChartMode mode = _mode;
        string symbol = _symbol;
        System.Threading.Tasks.Task.Factory.StartNew(delegate
        {
            string json = null;
            string error = null;
            try
            {
                json = mode == ChartMode.Minute
                    ? ChartClient.FetchMinute(symbol)
                    : ChartClient.FetchKline(symbol, PeriodOf(mode), 120);
            }
            catch
            {
                error = "加载失败，自动重试";
            }
            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    ApplyResponse(id, mode, symbol, json, error);
                }));
            }
            catch
            {
                _busy = false;
            }
        });
    }

    private void ApplyResponse(int id, ChartMode mode, string symbol, string json, string error)
    {
        if (_closed || id != _requestId) return;
        _busy = false;

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(json))
        {
            _canvas.SetState(false, "加载失败，自动重试");
            return;
        }

        try
        {
            if (mode == ChartMode.Minute)
            {
                MinuteSeries series = ChartParser.ParseMinute(symbol, json);
                _minute = series;
                if (series.Points.Count == 0)
                {
                    if (_mode == mode) _canvas.SetState(false, "暂无分时数据");
                }
                else if (_mode == mode)
                {
                    _canvas.SetMinute(series);
                    EnsureMinuteReference();
                }
            }
            else
            {
                DailySeries series = ChartParser.ParseKline(symbol, json, PeriodOf(mode));
                _klines[mode] = series;
                if (series.Bars.Count == 0)
                {
                    if (_mode == mode) _canvas.SetState(false, "暂无K线数据");
                }
                else
                {
                    if (mode == ChartMode.Daily)
                    {
                        if (_minute != null) _referenceDate = _minute.Date;
                        _referenceAttached = false;
                        if (_mode == ChartMode.Minute)
                        {
                            _canvas.SetMinuteReference(series);
                            _referenceAttached = true;
                        }
                    }
                    if (_mode == mode) _canvas.SetDaily(series);
                }
            }
            if (_mode == mode) UpdateHeader();
        }
        catch
        {
            if (_mode == mode) _canvas.SetState(false, "数据解析失败");
        }
    }

    private void EnsureMinuteReference()
    {
        if (_closed || _symbol.Length == 0) return;
        DailySeries cached;
        bool hasCached = _klines.TryGetValue(ChartMode.Daily, out cached) && cached.Bars.Count > 0;
        if (hasCached && (_referenceDate.Length == 0 || (_minute != null && _referenceDate == _minute.Date)))
        {
            if (_referenceDate.Length == 0 && _minute != null) _referenceDate = _minute.Date;
            if (_mode == ChartMode.Minute && !_referenceAttached)
            {
                _canvas.SetMinuteReference(cached);
                _referenceAttached = true;
            }
            return;
        }
        if (_minuteReferenceBusy || DateTime.Now < _minuteReferenceRetryAt) return;

        _minuteReferenceBusy = true;
        int id = ++_minuteReferenceId;
        string symbol = _symbol;
        string minuteDate = _minute != null ? _minute.Date : "";
        System.Threading.Tasks.Task.Factory.StartNew(delegate
        {
            string json = null;
            try
            {
                json = ChartClient.FetchKline(symbol, "day", 120);
            }
            catch { }
            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    ApplyMinuteReference(id, symbol, minuteDate, json);
                }));
            }
            catch
            {
                _minuteReferenceBusy = false;
            }
        });
    }

    private void ApplyMinuteReference(int id, string symbol, string minuteDate, string json)
    {
        if (_closed || id != _minuteReferenceId || symbol != _symbol) return;
        _minuteReferenceBusy = false;
        if (string.IsNullOrEmpty(json))
        {
            _minuteReferenceRetryAt = DateTime.Now.AddSeconds(60);
            return;
        }
        try
        {
            DailySeries series = ChartParser.ParseDaily(symbol, json);
            if (series.Bars.Count == 0)
            {
                _minuteReferenceRetryAt = DateTime.Now.AddSeconds(60);
                return;
            }
            _klines[ChartMode.Daily] = series;
            _referenceDate = minuteDate.Length > 0
                ? minuteDate : (_minute != null ? _minute.Date : "");
            _referenceAttached = false;
            if (_mode == ChartMode.Minute)
            {
                _canvas.SetMinuteReference(series);
                _referenceAttached = true;
            }
            _minuteReferenceRetryAt = DateTime.MinValue;
        }
        catch
        {
            _minuteReferenceRetryAt = DateTime.Now.AddSeconds(60);
        }
    }

    private void UpdateHeader()
    {
        ChartQuote quote = null;
        string date = "";
        if (_mode == ChartMode.Minute && _minute != null)
        {
            quote = _minute.Quote;
            date = _minute.Date;
        }
        else if (_mode != ChartMode.Minute)
        {
            DailySeries series;
            if (_klines.TryGetValue(_mode, out series))
            {
                quote = series.Quote;
                if (series.Bars.Count > 0)
                    date = series.Bars[series.Bars.Count - 1].Date;
            }
        }

        string name = _alias.Length > 0
            ? _alias
            : (quote != null && !string.IsNullOrEmpty(quote.Name) ? quote.Name : _code);
        _title.Text = name + "  " + _code;

        if (quote != null && quote.Price > 0)
        {
            _price.Text = FormatPrice(quote.Price) + "  " + FormatPct(quote.Pct);
            _price.ForeColor = quote.Pct > 0 ? ColorUp : (quote.Pct < 0 ? ColorDown : _nameColor);
        }
        else
        {
            _price.Text = "";
            _price.ForeColor = _nameColor;
        }
        _date.Text = FormatHeaderDate(date, _mode);
    }

    private void UpdateToggleStyles()
    {
        SetToggleStyle(_btnMinute, _mode == ChartMode.Minute);
        SetToggleStyle(_btnDaily, _mode == ChartMode.Daily);
        SetToggleStyle(_btnWeek, _mode == ChartMode.Weekly);
        SetToggleStyle(_btnMonth, _mode == ChartMode.Monthly);
    }

    private void SetToggleStyle(Label label, bool active)
    {
        label.Font = active ? _buttonActiveFont : _buttonFont;
        label.ForeColor = active ? _nameColor : ColorMuted;
    }

    private bool HasModeData(ChartMode mode)
    {
        if (mode == ChartMode.Minute)
            return _minute != null && _minute.Points.Count > 0;
        DailySeries series;
        return _klines.TryGetValue(mode, out series) && series.Bars.Count > 0;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        _closed = true;
        _requestId++;
        _minuteReferenceId++;
        _timer.Stop();
        SavePosition();
    }

    private void OnFormResize(object sender, EventArgs e)
    {
        _canvas.Bounds = new Rectangle(0, _headerHeight, ClientSize.Width,
            Math.Max(10, ClientSize.Height - _headerHeight));
        LayoutHeader();
        ApplyRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var brush = new SolidBrush(Shade(_bgColor, 1.12f)))
            e.Graphics.FillRectangle(brush, 0, 0, ClientSize.Width, _headerHeight);
        using (var pen = new Pen(ColorSeparator))
            e.Graphics.DrawLine(pen, 0, _headerHeight - 1, ClientSize.Width, _headerHeight - 1);
    }

    private void LoadPosition()
    {
        Point saved = PosConfig.Load(PosConfig.ChartPath);
        Rectangle area = Screen.FromRectangle(_widgetBounds).WorkingArea;
        if (saved == Point.Empty)
        {
            int gap = Round(12 * _scale);
            int x = _widgetBounds.Left - Width - gap;
            if (x < area.Left) x = _widgetBounds.Right + gap;
            if (x + Width > area.Right) x = area.Right - Width;
            int y = _widgetBounds.Top;
            if (y + Height > area.Bottom) y = area.Bottom - Height;
            Location = PosConfig.Clamp(new Point(x, y), area, Size);
        }
        else
        {
            Point center = new Point(saved.X + Width / 2, saved.Y + Height / 2);
            Rectangle savedArea = Screen.FromPoint(center).WorkingArea;
            Location = PosConfig.Clamp(saved, savedArea, Size);
        }
    }

    private void SavePosition()
    {
        try
        {
            Point center = new Point(Location.X + Width / 2, Location.Y + Height / 2);
            Rectangle area = Screen.FromPoint(center).WorkingArea;
            Location = PosConfig.Clamp(Location, area, Size);
            PosConfig.Save(PosConfig.ChartPath, Location);
        }
        catch { }
    }

    private void ApplyRoundedRegion()
    {
        int radius = Round(10 * _scale);
        int w = Width;
        int h = Height;
        if (w < radius * 2 || h < radius * 2)
        {
            ReplaceRegion(null);
            return;
        }
        using (var path = new GraphicsPath())
        {
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(w - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(w - radius * 2, h - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, h - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            ReplaceRegion(new Region(path));
        }
    }

    private void ReplaceRegion(Region region)
    {
        Region old = Region;
        Region = region;
        if (old != null) old.Dispose();
    }

    private static string FormatPrice(double value)
    {
        if (Math.Abs(value) < 10)
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatPct(double value)
    {
        return value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatHeaderDate(string date, ChartMode mode)
    {
        if (string.IsNullOrEmpty(date)) return "";
        if (mode == ChartMode.Monthly && date.Length >= 7) return date.Substring(0, 7);
        if (mode == ChartMode.Weekly && date.Length >= 10) return date.Substring(2, 8);
        if (date.Length == 8) return date.Substring(4, 2) + "-" + date.Substring(6, 2);
        if (date.Length >= 10) return date.Substring(5, 5);
        return date;
    }

    private static string PeriodOf(ChartMode mode)
    {
        if (mode == ChartMode.Weekly) return "week";
        if (mode == ChartMode.Monthly) return "month";
        return "day";
    }

    private static Color Shade(Color c, float factor)
    {
        int r = (int)Math.Min(255, Math.Max(0, c.R * factor));
        int g = (int)Math.Min(255, Math.Max(0, c.G * factor));
        int b = (int)Math.Min(255, Math.Max(0, c.B * factor));
        return Color.FromArgb(r, g, b);
    }

    private static int Round(float value)
    {
        return (int)Math.Round(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_timer != null) _timer.Dispose();
            _titleFont.Dispose();
            _smallFont.Dispose();
            _buttonFont.Dispose();
            _buttonActiveFont.Dispose();
            _closeFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
