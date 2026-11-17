using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class SearchForm : Form
{
    private static readonly Color ColorMuted = Color.FromArgb(140, 140, 150);
    private static readonly Color ColorAdded = Color.FromArgb(50, 190, 120);
    private static readonly Color ColorWarn = Color.FromArgb(255, 170, 60);
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly float _scale;
    private readonly Action<StockSearchResult> _onOpen;
    private readonly Func<StockSearchResult, StockListAddResult> _onAdd;
    private readonly TextBox _input;
    private readonly ListBox _list;
    private readonly Label _hint;
    private readonly Label _title;
    private readonly Label _close;
    private readonly Label _addButton;
    private readonly Label _openButton;
    private readonly Label _status;
    private readonly Timer _debounce;
    private readonly Font _titleFont;
    private readonly Font _inputFont;
    private readonly Font _rowFont;
    private readonly Font _closeFont;
    private readonly List<StockSearchResult> _results = new List<StockSearchResult>();
    private readonly HashSet<string> _addedSymbols = new HashSet<string>();
    private Color _bgColor = Color.FromArgb(32, 32, 36);
    private Color _nameColor = Color.WhiteSmoke;
    private Color _statusColor = ColorMuted;
    private int _requestId;
    private bool _closed;

    public SearchForm(UiMetrics metrics, Rectangle widgetBounds,
        Action<StockSearchResult> onOpen,
        Func<StockSearchResult, StockListAddResult> onAdd)
    {
        _scale = metrics.Scale;
        _onOpen = onOpen;
        _onAdd = onAdd;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = _bgColor;
        ClientSize = new Size(Round(360 * _scale), Round(300 * _scale));

        _titleFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        _inputFont = new Font("Microsoft YaHei UI", 10f);
        _rowFont = new Font("Microsoft YaHei UI", 10f);
        _closeFont = new Font("Microsoft YaHei UI", 12f);

        _title = CreateLabel(_titleFont, _nameColor, ContentAlignment.MiddleLeft);
        _title.Text = "搜索股票";
        _title.Bounds = new Rectangle(Round(12 * _scale), 0,
            Round(200 * _scale), Round(36 * _scale));

        _close = CreateLabel(_closeFont, ColorMuted, ContentAlignment.MiddleCenter);
        _close.Text = "\u00D7";
        _close.Cursor = Cursors.Hand;
        _close.Bounds = new Rectangle(ClientSize.Width - Round(40 * _scale), 0,
            Round(40 * _scale), Round(36 * _scale));
        _close.Click += delegate { Close(); };

        _input = new TextBox();
        _input.Font = _inputFont;
        _input.BorderStyle = BorderStyle.FixedSingle;
        _input.BackColor = Shade(_bgColor, 1.15f);
        _input.ForeColor = _nameColor;
        _input.Bounds = new Rectangle(Round(12 * _scale), Round(40 * _scale),
            ClientSize.Width - Round(24 * _scale), Round(30 * _scale));
        _input.TextChanged += OnInputTextChanged;
        Controls.Add(_input);

        int listTop = Round(80 * _scale);
        int listHeight = ClientSize.Height - listTop - Round(48 * _scale);
        var listRect = new Rectangle(Round(12 * _scale), listTop,
            ClientSize.Width - Round(24 * _scale), listHeight);

        _list = new ListBox();
        _list.Font = _rowFont;
        _list.BorderStyle = BorderStyle.None;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = Round(30 * _scale);
        _list.IntegralHeight = false;
        _list.BackColor = Shade(_bgColor, 1.08f);
        _list.ForeColor = _nameColor;
        _list.Bounds = listRect;
        _list.DrawItem += OnListDrawItem;
        _list.DoubleClick += delegate { OpenSelected(); };
        Controls.Add(_list);

        _hint = CreateLabel(_inputFont, ColorMuted, ContentAlignment.MiddleCenter);
        _hint.Bounds = listRect;
        _hint.Text = "输入代码、名称或拼音首字母";
        Controls.Add(_hint);

        int footerTop = ClientSize.Height - Round(40 * _scale);
        int buttonY = footerTop + Round(6 * _scale);
        int buttonWidth = Round(72 * _scale);
        int buttonHeight = Round(28 * _scale);

        _openButton = CreateLabel(_inputFont, ColorMuted, ContentAlignment.MiddleCenter);
        _openButton.Text = "打开图表";
        _openButton.Cursor = Cursors.Hand;
        _openButton.Bounds = new Rectangle(
            ClientSize.Width - Round(12 * _scale) - buttonWidth, buttonY,
            buttonWidth, buttonHeight);
        _openButton.Click += delegate { OpenSelected(); };

        _addButton = CreateLabel(_inputFont, ColorMuted, ContentAlignment.MiddleCenter);
        _addButton.Text = "加入列表";
        _addButton.Cursor = Cursors.Hand;
        _addButton.Bounds = new Rectangle(
            _openButton.Left - Round(8 * _scale) - buttonWidth, buttonY,
            buttonWidth, buttonHeight);
        _addButton.Click += delegate { AddSelected(); };

        _status = CreateLabel(_inputFont, ColorMuted, ContentAlignment.MiddleLeft);
        _status.Bounds = new Rectangle(
            Round(12 * _scale), footerTop,
            _addButton.Left - Round(20 * _scale), Round(40 * _scale));

        _debounce = new Timer();
        _debounce.Interval = 300;
        _debounce.Tick += OnDebounceTick;

        LoadPosition(widgetBounds);
        ApplyTheme(_bgColor, _nameColor);
        _list.Visible = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void ApplyTheme(Color background, Color nameColor)
    {
        _bgColor = background;
        _nameColor = nameColor;
        BackColor = background;
        _title.ForeColor = nameColor;
        _close.ForeColor = ColorMuted;
        _input.BackColor = Shade(background, 1.15f);
        _input.ForeColor = nameColor;
        _list.BackColor = Shade(background, 1.08f);
        _list.ForeColor = nameColor;
        _hint.ForeColor = ColorMuted;
        _addButton.ForeColor = ColorMuted;
        _openButton.ForeColor = ColorMuted;
        _status.ForeColor = _statusColor;
        Invalidate();
    }

    public void FocusInput()
    {
        if (_closed) return;
        Activate();
        _input.Focus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FocusInput();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            Close();
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (e.Control) AddSelected();
            else OpenSelected();
            return;
        }
        if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveSelection(e.KeyCode == Keys.Down ? 1 : -1);
        }
    }

    private void MoveSelection(int delta)
    {
        if (_list.Items.Count == 0) return;
        int index = _list.SelectedIndex + delta;
        if (index < 0) index = 0;
        if (index >= _list.Items.Count) index = _list.Items.Count - 1;
        _list.SelectedIndex = index;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closed = true;
        _requestId++;
        _debounce.Stop();
        base.OnFormClosing(e);
    }

    private void OnInputTextChanged(object sender, EventArgs e)
    {
        SetStatus("", ColorMuted);
        _requestId++;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceTick(object sender, EventArgs e)
    {
        _debounce.Stop();
        string query = _input.Text.Trim();
        if (query.Length == 0)
        {
            ClearResults("输入代码、名称或拼音首字母");
            return;
        }
        StartSearch(query);
    }

    private void StartSearch(string query)
    {
        int id = ++_requestId;
        System.Threading.Tasks.Task.Factory.StartNew(delegate
        {
            string json = null;
            bool ok = false;
            try
            {
                json = StockSearchClient.FetchRaw(query);
                ok = true;
            }
            catch { }
            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    ApplyResponse(id, json, ok);
                }));
            }
            catch { }
        });
    }

    private void ApplyResponse(int id, string json, bool ok)
    {
        if (_closed || IsDisposed || id != _requestId) return;
        if (!ok || string.IsNullOrEmpty(json))
        {
            ClearResults("搜索失败，请重试");
            return;
        }
        List<StockSearchResult> parsed;
        try
        {
            parsed = StockSearchParser.Parse(json);
        }
        catch
        {
            ClearResults("搜索失败，请重试");
            return;
        }
        if (parsed.Count == 0)
        {
            ClearResults("无匹配结果");
            return;
        }

        _results.Clear();
        _results.AddRange(parsed);
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            for (int i = 0; i < _results.Count; i++) _list.Items.Add(_results[i]);
            _list.SelectedIndex = 0;
        }
        finally
        {
            _list.EndUpdate();
        }
        _hint.Visible = false;
        _list.Visible = true;
    }

    private void ClearResults(string hint)
    {
        _results.Clear();
        _list.Items.Clear();
        _list.Visible = false;
        _hint.Text = hint;
        _hint.Visible = true;
        _hint.BringToFront();
    }

    private void AddSelected()
    {
        StockSearchResult result = _list.SelectedItem as StockSearchResult;
        if (result == null)
        {
            SetStatus("先选择一只股票", ColorMuted);
            return;
        }

        StockListAddResult outcome = _onAdd != null
            ? _onAdd(result) : StockListAddResult.Failed;
        if (outcome == StockListAddResult.Added)
        {
            _addedSymbols.Add(result.Symbol);
            SetStatus("已加入列表", ColorAdded);
        }
        else if (outcome == StockListAddResult.AlreadyExists)
        {
            _addedSymbols.Add(result.Symbol);
            SetStatus("已在列表中", ColorMuted);
        }
        else
        {
            SetStatus("加入失败", ColorWarn);
        }
        _list.Invalidate();
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _statusColor = color;
        _status.ForeColor = color;
    }

    private void OpenSelected()
    {
        StockSearchResult result = _list.SelectedItem as StockSearchResult;
        if (result == null)
        {
            SetStatus("先选择一只股票", ColorMuted);
            return;
        }
        Action<StockSearchResult> callback = _onOpen;
        Close();
        if (callback != null) callback(result);
    }

    private void OnListDrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _results.Count) return;
        StockSearchResult result = _results[e.Index];
        Graphics g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using (var back = new SolidBrush(selected ? Shade(_bgColor, 1.30f) : Shade(_bgColor, 1.08f)))
            g.FillRectangle(back, e.Bounds);

        int pad = Round(8 * _scale);
        var row = new Rectangle(e.Bounds.Left + pad, e.Bounds.Top,
            e.Bounds.Width - pad * 2, e.Bounds.Height);
        int typeWidth = Round(44 * _scale);
        int codeWidth = Round(58 * _scale);
        int nameWidth = row.Width - typeWidth - codeWidth - pad * 2;
        if (nameWidth < Round(40 * _scale)) nameWidth = Round(40 * _scale);

        TextRenderer.DrawText(g, result.Name, _rowFont,
            new Rectangle(row.Left, row.Top, nameWidth, row.Height),
            _nameColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, result.Code, _rowFont,
            new Rectangle(row.Left + nameWidth + pad, row.Top, codeWidth, row.Height),
            ColorMuted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix);
        bool added = _addedSymbols.Contains(result.Symbol);
        TextRenderer.DrawText(g, added ? "已加入" : StockSearchParser.TypeLabel(result.Type), _rowFont,
            new Rectangle(row.Right - typeWidth, row.Top, typeWidth, row.Height),
            added ? ColorAdded : ColorMuted, TextFormatFlags.Right |
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private void LoadPosition(Rectangle widgetBounds)
    {
        Rectangle area = Screen.FromRectangle(widgetBounds).WorkingArea;
        Location = new Point(
            area.Left + (area.Width - Width) / 2,
            area.Top + (area.Height - Height) / 2);
    }

    private Label CreateLabel(Font font, Color color, ContentAlignment align)
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
            if (_debounce != null) _debounce.Dispose();
            if (_titleFont != null) _titleFont.Dispose();
            if (_inputFont != null) _inputFont.Dispose();
            if (_rowFont != null) _rowFont.Dispose();
            if (_closeFont != null) _closeFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
