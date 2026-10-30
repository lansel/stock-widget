using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class RowView : IDisposable
{
    private static readonly Color ColorUp = Color.FromArgb(230, 70, 70);
    private static readonly Color ColorDown = Color.FromArgb(50, 190, 120);
    private static readonly Color ColorFlat = Color.Gray;
    private static readonly Color ColorStale = Color.FromArgb(255, 170, 60);

    private readonly Control _parent;
    private readonly UiMetrics _metrics;
    private readonly Action<Control> _configure;
    private readonly Font _rowFont;
    private readonly Font _hintFont;
    private readonly Font _dotFont;
    private readonly ToolTip _toolTip;
    private readonly List<Label> _nameLabels = new List<Label>();
    private readonly List<Label> _priceLabels = new List<Label>();
    private readonly List<Label> _pctLabels = new List<Label>();
    private Label _hint;
    private Label _closedHint;
    private Label _statusDot;
    private List<string> _codes = new List<string>();
    private List<string> _aliases = new List<string>();
    private Color _nameColor = Color.WhiteSmoke;
    private bool _showPrice;
    private bool _marketClosed;
    private bool _fileExists;
    private bool _stale;
    private bool _built;

    public RowView(Control parent, UiMetrics metrics, Action<Control> configureTarget)
    {
        _parent = parent;
        _metrics = metrics;
        _configure = configureTarget;
        _rowFont = new Font("Microsoft YaHei UI", 10f);
        _hintFont = new Font("Microsoft YaHei UI", 9.5f);
        _dotFont = new Font("Microsoft YaHei UI", 8f);
        _toolTip = new ToolTip();

        _statusDot = new Label();
        _statusDot.Text = "\u25CF";
        _statusDot.Font = _dotFont;
        _statusDot.ForeColor = ColorStale;
        _statusDot.BackColor = Color.Transparent;
        _statusDot.AutoSize = true;
        _statusDot.Visible = false;
        _statusDot.UseMnemonic = false;
        _configure(_statusDot);
        _parent.Controls.Add(_statusDot);
        _toolTip.SetToolTip(_statusDot, "行情未更新");
    }

    public bool SetCodes(IList<string> codes, IList<string> aliases, bool showPrice, bool fileExists)
    {
        if (codes == null) codes = new List<string>();
        bool same = _built && SameCodes(_codes, codes)
            && SameCodes(_aliases, aliases) && _showPrice == showPrice;
        if (same)
        {
            bool hintChanged = false;
            if (_codes.Count == 0 && _fileExists != fileExists)
            {
                _fileExists = fileExists;
                EnsureHint();
                UpdateHintText();
                Layout();
                hintChanged = true;
            }
            else if (_fileExists != fileExists)
            {
                _fileExists = fileExists;
            }
            return hintChanged;
        }

        ClearRows();
        _codes = new List<string>(codes);
        _aliases = new List<string>();
        for (int i = 0; i < _codes.Count; i++)
            _aliases.Add(aliases != null && i < aliases.Count && aliases[i] != null ? aliases[i] : "");
        _showPrice = showPrice;
        _fileExists = fileExists;
        if (_codes.Count == 0)
        {
            EnsureHint();
        }
        else
        {
            for (int i = 0; i < _codes.Count; i++) CreateRow(_codes[i], _aliases[i]);
        }
        _built = true;
        UpdateStatusDot();
        ApplyClosedVisibility();
        Layout();
        return true;
    }

    public void SetNameColor(Color color)
    {
        _nameColor = color;
        for (int i = 0; i < _nameLabels.Count; i++)
        {
            if (_nameLabels[i].ForeColor != color) _nameLabels[i].ForeColor = color;
        }
        for (int i = 0; i < _priceLabels.Count; i++)
        {
            if (_priceLabels[i].ForeColor != color) _priceLabels[i].ForeColor = color;
        }
    }

    public void Render(IDictionary<string, Quote> quotes)
    {
        for (int i = 0; i < _codes.Count && i < _nameLabels.Count; i++)
        {
            string code = _codes[i];
            string symbol = StockList.ToSymbol(code);
            Quote q;
            if (quotes != null && quotes.TryGetValue(symbol, out q))
            {
                string display = _aliases[i].Length > 0 ? _aliases[i] : q.Name;
                SetLabel(_nameLabels[i], display, _nameColor);
                _toolTip.SetToolTip(_nameLabels[i], ToolTipText(i, q.Name));
                if (_showPrice && i < _priceLabels.Count)
                    SetLabel(_priceLabels[i],
                        q.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                        _nameColor);
                SetLabel(_pctLabels[i], QuoteParser.FormatPct(q.Pct),
                    q.Pct > 0 ? ColorUp : (q.Pct < 0 ? ColorDown : ColorFlat));
            }
            else
            {
                SetLabel(_nameLabels[i], _aliases[i].Length > 0 ? _aliases[i] : code, _nameColor);
                _toolTip.SetToolTip(_nameLabels[i], ToolTipText(i, code));
                if (_showPrice && i < _priceLabels.Count)
                    SetLabel(_priceLabels[i], "--", _nameColor);
                SetLabel(_pctLabels[i], "无数据", ColorFlat);
            }
        }
    }

    // 有别名时 tooltip 带出代码和服务器名称，信息不丢
    private string ToolTipText(int i, string serverName)
    {
        if (i >= _aliases.Count || _aliases[i].Length == 0) return serverName;
        return _aliases[i] + " (" + _codes[i] + " " + serverName + ")";
    }

    public void SetStale(bool stale)
    {
        _stale = stale;
        UpdateStatusDot();
    }

    // 非开盘日（周末/法定假日）：隐藏行情行，显示“今日休市”单行提示
    public void SetMarketClosed(bool closed)
    {
        if (_marketClosed == closed) return;
        _marketClosed = closed;
        ApplyClosedVisibility();
        Layout();
    }

    private void ApplyClosedVisibility()
    {
        bool showRows = !_marketClosed || _codes.Count == 0;
        for (int i = 0; i < _nameLabels.Count; i++) _nameLabels[i].Visible = showRows;
        for (int i = 0; i < _priceLabels.Count; i++) _priceLabels[i].Visible = showRows;
        for (int i = 0; i < _pctLabels.Count; i++) _pctLabels[i].Visible = showRows;
        if (showRows)
        {
            if (_closedHint != null) _closedHint.Visible = false;
        }
        else
        {
            EnsureClosedHint();
            _closedHint.Visible = true;
        }
    }

    private void EnsureClosedHint()
    {
        if (_closedHint != null) return;
        _closedHint = new Label();
        _closedHint.Font = _hintFont;
        _closedHint.ForeColor = Color.Gray;
        _closedHint.AutoSize = false;
        _closedHint.AutoEllipsis = true;
        _closedHint.UseMnemonic = false;
        _closedHint.TextAlign = ContentAlignment.MiddleLeft;
        _closedHint.Text = "今日休市";
        _configure(_closedHint);
        _parent.Controls.Add(_closedHint);
    }

    private void UpdateStatusDot()
    {
        _statusDot.Visible = _stale && _codes.Count > 0 && !_marketClosed;
        if (_statusDot.Visible) _statusDot.BringToFront();
    }

    private void CreateRow(string code, string alias)
    {
        var name = new Label();
        name.Font = _rowFont;
        name.ForeColor = _nameColor;
        name.AutoSize = false;
        name.AutoEllipsis = true;
        name.UseMnemonic = false;
        name.TextAlign = ContentAlignment.MiddleLeft;
        name.Size = new Size(_metrics.NameColumnWidth, _metrics.RowHeight);
        name.Text = alias.Length > 0 ? alias : code;
        _configure(name);
        _parent.Controls.Add(name);
        _nameLabels.Add(name);

        if (_showPrice)
        {
            var price = new Label();
            price.Font = _rowFont;
            price.ForeColor = _nameColor;
            price.AutoSize = false;
            price.AutoEllipsis = true;
            price.UseMnemonic = false;
            price.TextAlign = ContentAlignment.MiddleRight;
            price.Size = new Size(_metrics.PriceColumnWidth, _metrics.RowHeight);
            price.Text = "--";
            _configure(price);
            _parent.Controls.Add(price);
            _priceLabels.Add(price);
        }

        var pct = new Label();
        pct.Font = _rowFont;
        pct.AutoSize = false;
        pct.UseMnemonic = false;
        pct.TextAlign = ContentAlignment.MiddleRight;
        pct.Size = new Size(_metrics.PctColumnWidth, _metrics.RowHeight);
        pct.Text = "--";
        pct.ForeColor = ColorFlat;
        _configure(pct);
        _parent.Controls.Add(pct);
        _pctLabels.Add(pct);
    }

    private void EnsureHint()
    {
        if (_hint != null) return;
        _hint = new Label();
        _hint.Font = _hintFont;
        _hint.ForeColor = Color.Gray;
        _hint.AutoSize = false;
        _hint.AutoEllipsis = true;
        _hint.UseMnemonic = false;
        _hint.TextAlign = ContentAlignment.MiddleLeft;
        _hint.Size = new Size(_metrics.NameColumnWidth + _metrics.PctColumnWidth, _metrics.RowHeight);
        UpdateHintText();
        _configure(_hint);
        _parent.Controls.Add(_hint);
    }

    private void UpdateHintText()
    {
        if (_hint == null) return;
        _hint.Text = _fileExists ? "请编辑 stocks.txt" : "未找到 stocks.txt";
    }

    private void Layout()
    {
        int priceW = _showPrice ? _metrics.PriceColumnWidth : 0;
        int width = _metrics.Width + priceW;
        int h;
        if (_marketClosed && _codes.Count > 0)
        {
            _closedHint.Size = new Size(width - _metrics.MarginX * 2, _metrics.RowHeight);
            _closedHint.Location = new Point(_metrics.MarginX, _metrics.MarginY);
            h = _metrics.MarginY * 2 + _metrics.RowHeight;
        }
        else if (_hint != null)
        {
            _hint.Size = new Size(width - _metrics.MarginX * 2, _metrics.RowHeight);
            _hint.Location = new Point(_metrics.MarginX, _metrics.MarginY);
            h = _metrics.MarginY + _metrics.RowHeight + _metrics.MarginY;
        }
        else
        {
            int y = _metrics.MarginY;
            for (int i = 0; i < _nameLabels.Count; i++)
            {
                _nameLabels[i].Location = new Point(_metrics.MarginX, y);
                if (_showPrice && i < _priceLabels.Count)
                    _priceLabels[i].Location =
                        new Point(_metrics.MarginX + _metrics.NameColumnWidth, y);
                _pctLabels[i].Location = new Point(
                    _metrics.MarginX + _metrics.NameColumnWidth + priceW, y);
                y += _metrics.RowHeight;
            }
            h = y + _metrics.MarginY;
        }
        if (h < _metrics.MinHeight) h = _metrics.MinHeight;
        _parent.Size = new Size(width, h);

        _statusDot.Location = new Point(width - _metrics.DotRight, _metrics.DotTop);
        if (_statusDot.Visible) _statusDot.BringToFront();
    }

    private void ClearRows()
    {
        for (int i = 0; i < _nameLabels.Count; i++) RemoveAndDispose(_nameLabels[i]);
        for (int i = 0; i < _priceLabels.Count; i++) RemoveAndDispose(_priceLabels[i]);
        for (int i = 0; i < _pctLabels.Count; i++) RemoveAndDispose(_pctLabels[i]);
        _nameLabels.Clear();
        _priceLabels.Clear();
        _pctLabels.Clear();
        _aliases.Clear();
        if (_hint != null)
        {
            RemoveAndDispose(_hint);
            _hint = null;
        }
    }

    private void RemoveAndDispose(Control c)
    {
        _parent.Controls.Remove(c);
        c.Dispose();
    }

    private static void SetLabel(Label label, string text, Color color)
    {
        if (label.Text != text) label.Text = text;
        if (label.ForeColor != color) label.ForeColor = color;
    }

    private static bool SameCodes(IList<string> a, IList<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    public void Dispose()
    {
        ClearRows();
        if (_closedHint != null)
        {
            RemoveAndDispose(_closedHint);
            _closedHint = null;
        }
        if (_statusDot != null)
        {
            RemoveAndDispose(_statusDot);
            _statusDot = null;
        }
        _toolTip.Dispose();
        _rowFont.Dispose();
        _hintFont.Dispose();
        _dotFont.Dispose();
    }
}
