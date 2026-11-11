using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

public enum ChartMode
{
    Minute,
    Daily,
    Weekly,
    Monthly
}

public sealed class ChartCanvas : Control
{
    private static readonly Color ColorUp = Color.FromArgb(230, 70, 70);
    private static readonly Color ColorDown = Color.FromArgb(50, 190, 120);
    private static readonly Color ColorFlat = Color.Gray;
    private static readonly Color ColorAvg = Color.FromArgb(232, 193, 90);
    private static readonly Color ColorGrid = Color.FromArgb(58, 58, 64);
    private static readonly Color ColorAxis = Color.FromArgb(150, 150, 158);
    private static readonly Color ColorError = Color.FromArgb(255, 170, 60);
    private static readonly Color ColorMa5 = Color.FromArgb(235, 235, 240);
    private static readonly Color ColorMa10 = Color.FromArgb(232, 193, 90);
    private static readonly Color ColorMa20 = Color.FromArgb(200, 130, 230);
    private static readonly Color ColorCross = Color.FromArgb(130, 130, 140);
    private static readonly Color ColorLimitUp = Color.FromArgb(255, 196, 58);
    private static readonly Color ColorLimitMarker = Color.White;
    private static readonly Color ColorDayMa3 = Color.FromArgb(95, 211, 243);
    private static readonly Color ColorDayMa5 = Color.FromArgb(255, 143, 208);
    private static readonly Color ColorDayMa10 = Color.FromArgb(155, 227, 123);
    private static readonly Color ColorDayMa20 = Color.FromArgb(200, 130, 230);

    private readonly Font _axisFont;
    private readonly Font _legendFont;
    private readonly Font _readoutFont;
    private readonly Font _messageFont;

    private ChartMode _mode = ChartMode.Minute;
    private MinuteSeries _minute;
    private DailySeries _daily;
    private DailySeries _minuteReference;
    private bool _loading;
    private string _error;
    private Color _bg = Color.FromArgb(32, 32, 36);
    private Color _text = Color.WhiteSmoke;
    private float _scale = 1f;
    private Point _mouse = new Point(-1, -1);
    private bool _mouseIn;

    public ChartCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw, true);
        BackColor = _bg;
        TabStop = false;
        _axisFont = new Font("Microsoft YaHei UI", 8f);
        _legendFont = new Font("Microsoft YaHei UI", 8f);
        _readoutFont = new Font("Microsoft YaHei UI", 8.5f);
        _messageFont = new Font("Microsoft YaHei UI", 10f);
    }

    public ChartMode Mode
    {
        get { return _mode; }
        set
        {
            if (_mode == value) return;
            _mode = value;
            Invalidate();
        }
    }

    public float UiScale
    {
        get { return _scale; }
        set { if (value > 0.1f) _scale = value; }
    }

    public void SetMinute(MinuteSeries series)
    {
        _minute = series;
        _loading = false;
        _error = null;
        Invalidate();
    }

    public void SetDaily(DailySeries series)
    {
        _daily = series;
        _loading = false;
        _error = null;
        Invalidate();
    }

    public void SetMinuteReference(DailySeries series)
    {
        _minuteReference = series;
        Invalidate();
    }

    public void SetState(bool loading, string error)
    {
        _loading = loading;
        _error = error;
        Invalidate();
    }

    public void Clear()
    {
        _minute = null;
        _daily = null;
        _minuteReference = null;
        _error = null;
        _loading = true;
        Invalidate();
    }

    public void ApplyTheme(Color background, Color text)
    {
        _bg = background;
        _text = text;
        BackColor = background;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _mouse = e.Location;
        _mouseIn = true;
        Invalidate();
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _mouseIn = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (var brush = new SolidBrush(_bg))
            g.FillRectangle(brush, ClientRectangle);

        if (_mode == ChartMode.Minute) PaintMinute(g);
        else PaintDaily(g);

        if (_error != null && HasData())
        {
            Rectangle plot = PlotRect();
            using (var brush = new SolidBrush(ColorError))
                g.DrawString(_error, _axisFont, brush,
                    plot.Left + 4 * _scale, plot.Top + 2 * _scale);
        }
    }

    private bool HasData()
    {
        if (_mode == ChartMode.Minute)
            return _minute != null && _minute.Points.Count > 0;
        return _daily != null && _daily.Bars.Count > 0;
    }

    private Rectangle PlotRect()
    {
        int left = Round(54 * _scale);
        int right = Round((_mode == ChartMode.Minute ? 52 : 14) * _scale);
        int top = Round(12 * _scale);
        int bottom = Round(22 * _scale);
        int w = Width - left - right;
        int h = Height - top - bottom;
        if (w < 20) w = 20;
        if (h < 20) h = 20;
        return new Rectangle(left, top, w, h);
    }

    private Rectangle SplitPriceRect(Rectangle plot)
    {
        int gap = Round(7 * _scale);
        int volumeHeight = Round(plot.Height * 0.26f);
        int minVolume = Round(44 * _scale);
        int minPrice = Round(70 * _scale);
        if (volumeHeight < minVolume) volumeHeight = minVolume;
        int maxVolume = plot.Height - gap - minPrice;
        if (maxVolume < 10) maxVolume = 10;
        if (volumeHeight > maxVolume) volumeHeight = maxVolume;
        int priceHeight = plot.Height - gap - volumeHeight;
        if (priceHeight < 10) priceHeight = 10;
        return new Rectangle(plot.Left, plot.Top, plot.Width, priceHeight);
    }

    private Rectangle SplitVolumeRect(Rectangle plot)
    {
        Rectangle price = SplitPriceRect(plot);
        int top = price.Bottom + Round(7 * _scale);
        int height = plot.Bottom - top;
        if (height < 1) height = 1;
        return new Rectangle(plot.Left, top, plot.Width, height);
    }

    private void PaintMinute(Graphics g)
    {
        MinuteSeries s = _minute;
        if (s == null || s.Points.Count == 0)
        {
            DrawCenterMessage(g, _loading ? "加载中…" : (_error != null ? _error : "暂无分时数据"),
                _error != null ? ColorError : ColorAxis);
            return;
        }

        Rectangle plot = PlotRect();
        Rectangle pricePlot = SplitPriceRect(plot);
        Rectangle volumePlot = SplitVolumeRect(plot);
        double prev = s.Quote != null ? s.Quote.PrevClose : 0;
        double[] volumes = ChartScale.MinuteVolumes(s.Points);
        double maxVolume = 0;
        for (int i = 0; i < volumes.Length; i++)
            if (volumes[i] > maxVolume) maxVolume = volumes[i];
        int[] trends = new int[volumes.Length];
        double trendPrice = prev;
        for (int i = 0; i < s.Points.Count; i++)
        {
            MinutePoint p = s.Points[i];
            if (p.MinuteIndex >= 0 && p.MinuteIndex < trends.Length)
            {
                if (trendPrice > 0 && p.Price > trendPrice) trends[p.MinuteIndex] = 1;
                else if (trendPrice > 0 && p.Price < trendPrice) trends[p.MinuteIndex] = -1;
            }
            trendPrice = p.Price;
        }
        double min = double.MaxValue;
        double max = double.MinValue;
        for (int i = 0; i < s.Points.Count; i++)
        {
            MinutePoint p = s.Points[i];
            if (p.Price < min) min = p.Price;
            if (p.Price > max) max = p.Price;
            if (s.HasAvg && p.Avg > 0)
            {
                if (p.Avg < min) min = p.Avg;
                if (p.Avg > max) max = p.Avg;
            }
        }
        if (prev > 0)
        {
            if (prev < min) min = prev;
            if (prev > max) max = prev;
        }
        if (min > max) { min = 0; max = 1; }
        double pad = (max - min) * 0.05;
        if (pad <= 0) pad = Math.Max(0.01, Math.Abs(max) * 0.001);
        min -= pad;
        max += pad;

        double ma3 = double.NaN;
        double ma5 = double.NaN;
        double ma10 = double.NaN;
        double ma20 = double.NaN;
        if (_minuteReference != null && _minuteReference.Bars.Count > 0)
        {
            double current = s.Quote != null && s.Quote.Price > 0
                ? s.Quote.Price : s.Points[s.Points.Count - 1].Price;
            string tradeDate = !string.IsNullOrEmpty(s.Date)
                ? s.Date : DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            ma3 = ChartScale.IntradayMa(_minuteReference.Bars, tradeDate, current, 3);
            ma5 = ChartScale.IntradayMa(_minuteReference.Bars, tradeDate, current, 5);
            ma10 = ChartScale.IntradayMa(_minuteReference.Bars, tradeDate, current, 10);
            ma20 = ChartScale.IntradayMa(_minuteReference.Bars, tradeDate, current, 20);
        }

        List<double> ticks = ChartScale.NiceTicks(min, max, 5);
        using (var gridPen = new Pen(ColorGrid, 1f))
        using (var axisBrush = new SolidBrush(ColorAxis))
        {
            for (int i = 0; i < ticks.Count; i++)
            {
                double t = ticks[i];
                int y = Round(PriceToY(t, min, max, pricePlot));
                g.DrawLine(gridPen, pricePlot.Left, y, pricePlot.Right, y);
                string label = FormatPrice(t);
                SizeF sz = g.MeasureString(label, _axisFont);
                g.DrawString(label, _axisFont, axisBrush,
                    pricePlot.Left - sz.Width - 4 * _scale, y - sz.Height / 2f);
                if (prev > 0)
                {
                    string pct = FormatPct(ChartScale.PercentFromPrev(t, prev));
                    g.DrawString(pct, _axisFont, axisBrush,
                        pricePlot.Right + 5 * _scale, y - sz.Height / 2f);
                }
            }

            int[] marks = { 0, 60, 120, 180, 240 };
            string[] labels = { "09:30", "10:30", "11:30/13:00", "14:00", "15:00" };
            for (int i = 0; i < marks.Length; i++)
            {
                float x = MinuteX(marks[i], plot);
                g.DrawLine(gridPen, x, pricePlot.Top, x, volumePlot.Bottom);
                SizeF sz = g.MeasureString(labels[i], _axisFont);
                float tx = x - sz.Width / 2f;
                if (tx < plot.Left) tx = plot.Left;
                if (tx + sz.Width > plot.Right) tx = plot.Right - sz.Width;
                g.DrawString(labels[i], _axisFont, axisBrush, tx, plot.Bottom + 4 * _scale);
            }
        }

        if (prev > 0)
        {
            using (var pen = new Pen(Color.FromArgb(100, 100, 108), 1f))
            {
                pen.DashStyle = DashStyle.Dash;
                int y = Round(PriceToY(prev, min, max, pricePlot));
                g.DrawLine(pen, pricePlot.Left, y, pricePlot.Right, y);
            }
        }

        DrawMinuteMaLines(g, pricePlot, min, max, ma3, ma5, ma10, ma20);

        var pricePoints = new List<PointF>();
        for (int i = 0; i < s.Points.Count; i++)
        {
            MinutePoint p = s.Points[i];
            pricePoints.Add(new PointF(
                MinuteX(p.MinuteIndex, plot),
                PriceToY(p.Price, min, max, pricePlot)));
        }
        DrawPolyline(g, pricePoints, _text, 1.4f * _scale);

        if (s.HasAvg)
        {
            var avgPoints = new List<PointF>();
            for (int i = 0; i < s.Points.Count; i++)
            {
                MinutePoint p = s.Points[i];
                avgPoints.Add(new PointF(
                    MinuteX(p.MinuteIndex, plot),
                    PriceToY(p.Avg, min, max, pricePlot)));
            }
            DrawPolyline(g, avgPoints, ColorAvg, 1.2f * _scale);
        }

        DrawMinuteMaLegend(g, pricePlot, min, max, ma3, ma5, ma10, ma20);
        DrawMinuteVolume(g, plot, volumePlot, volumes, trends, maxVolume);

        if (_mouseIn && plot.Contains(_mouse))
            PaintMinuteCrosshair(g, s, plot, pricePlot, volumePlot, min, max, prev, volumes, maxVolume);
    }

    private void DrawMinuteMaLines(Graphics g, Rectangle pricePlot, double min, double max,
        double ma3, double ma5, double ma10, double ma20)
    {
        DrawMinuteMaLine(g, pricePlot, min, max, ma3, ColorDayMa3);
        DrawMinuteMaLine(g, pricePlot, min, max, ma5, ColorDayMa5);
        DrawMinuteMaLine(g, pricePlot, min, max, ma10, ColorDayMa10);
        DrawMinuteMaLine(g, pricePlot, min, max, ma20, ColorDayMa20);
    }

    private void DrawMinuteMaLine(Graphics g, Rectangle pricePlot, double min, double max,
        double value, Color color)
    {
        if (double.IsNaN(value) || value < min || value > max) return;
        float y = PriceToY(value, min, max, pricePlot);
        using (var pen = new Pen(Color.FromArgb(190, color), Math.Max(1f, _scale)))
        {
            pen.DashStyle = DashStyle.Dash;
            g.DrawLine(pen, pricePlot.Left, y, pricePlot.Right, y);
        }
    }

    private void DrawMinuteMaLegend(Graphics g, Rectangle pricePlot, double min, double max,
        double ma3, double ma5, double ma10, double ma20)
    {
        if (_minuteReference == null) return;
        string[] labels = { "3日", "5日", "10日", "20日" };
        double[] values = { ma3, ma5, ma10, ma20 };
        Color[] colors = { ColorDayMa3, ColorDayMa5, ColorDayMa10, ColorDayMa20 };
        string[] parts = new string[labels.Length];
        float[] widths = new float[labels.Length];
        float total = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            string valueText = double.IsNaN(values[i]) ? "--" : FormatPrice(values[i]);
            string arrow = "";
            if (!double.IsNaN(values[i]))
            {
                if (values[i] > max) arrow = "↑";
                else if (values[i] < min) arrow = "↓";
            }
            parts[i] = labels[i] + " " + valueText + arrow;
            widths[i] = g.MeasureString(parts[i], _legendFont).Width;
            total += widths[i];
            if (i < labels.Length - 1) total += 8 * _scale;
        }

        float x = pricePlot.Left + 6 * _scale;
        float y = pricePlot.Top + 2 * _scale;
        float height = _legendFont.GetHeight(g) + 2 * _scale;
        using (var bg = new SolidBrush(Color.FromArgb(220, Shade(_bg, 1.2f))))
            g.FillRectangle(bg, x - 4 * _scale, y - 1 * _scale, total + 8 * _scale, height);
        for (int i = 0; i < labels.Length; i++)
        {
            using (var brush = new SolidBrush(colors[i]))
                g.DrawString(parts[i], _legendFont, brush, x, y);
            x += widths[i] + 8 * _scale;
        }
    }

    private void PaintMinuteCrosshair(Graphics g, MinuteSeries s, Rectangle plot,
        Rectangle pricePlot, Rectangle volumePlot, double min, double max, double prev,
        double[] volumes, double maxVolume)
    {
        int target = Round((_mouse.X - plot.Left) / (float)plot.Width * 240);
        if (target < 0) target = 0;
        if (target > 240) target = 240;
        MinutePoint best = null;
        int bestDist = int.MaxValue;
        for (int i = 0; i < s.Points.Count; i++)
        {
            int d = Math.Abs(s.Points[i].MinuteIndex - target);
            if (d <= bestDist)
            {
                bestDist = d;
                best = s.Points[i];
            }
        }
        if (best == null) return;

        float x = MinuteX(best.MinuteIndex, plot);
        float y = PriceToY(best.Price, min, max, pricePlot);
        double minuteVolume = best.MinuteIndex >= 0 && best.MinuteIndex < volumes.Length
            ? volumes[best.MinuteIndex] : 0;
        Rectangle cross = Rectangle.FromLTRB(plot.Left, pricePlot.Top, plot.Right, volumePlot.Bottom);
        if (_mouse.Y > pricePlot.Bottom && _mouse.Y <= volumePlot.Bottom)
        {
            float volumeY = VolumeToY(minuteVolume, maxVolume, volumePlot);
            DrawCrossLines(g, cross, volumePlot, x, volumeY);
        }
        else
        {
            DrawCrossLines(g, cross, pricePlot, x, y);
        }

        var lines = new List<string>();
        lines.Add(FormatTime(best.Time));
        lines.Add("价格 " + FormatPrice(best.Price));
        if (s.HasAvg) lines.Add("均价 " + FormatPrice(best.Avg));
        lines.Add("量 " + ChartScale.FormatVolume(minuteVolume) + "手");
        if (prev > 0)
            lines.Add("涨跌 " + FormatPct(ChartScale.PercentFromPrev(best.Price, prev)));
        DrawReadout(g, lines, plot, new PointF(x, y));
    }

    private void PaintDaily(Graphics g)
    {
        DailySeries s = _daily;
        if (s == null || s.Bars.Count == 0)
        {
            DrawCenterMessage(g, _loading ? "加载中…" : (_error != null ? _error : "暂无K线数据"),
                _error != null ? ColorError : ColorAxis);
            return;
        }

        Rectangle plot = PlotRect();
        Rectangle pricePlot = SplitPriceRect(plot);
        Rectangle volumePlot = SplitVolumeRect(plot);
        int slot = Math.Max(4, Round(9 * _scale));
        int visible = plot.Width / slot;
        if (visible < 10) visible = Math.Min(10, s.Bars.Count);
        if (visible > s.Bars.Count) visible = s.Bars.Count;
        if (visible < 1) visible = 1;
        int start = ChartScale.VisibleStart(s.Bars.Count, visible);

        double[] ma5 = ChartScale.MovingAverage(s.Bars, 5);
        double[] ma10 = ChartScale.MovingAverage(s.Bars, 10);
        double[] ma20 = ChartScale.MovingAverage(s.Bars, 20);
        double maxVolume = ChartScale.MaxVolume(s.Bars, start);

        double min = double.MaxValue;
        double max = double.MinValue;
        for (int i = start; i < s.Bars.Count; i++)
        {
            DailyBar b = s.Bars[i];
            if (b.Low < min) min = b.Low;
            if (b.High > max) max = b.High;
            IncludeMa(ma5, i, ref min, ref max);
            IncludeMa(ma10, i, ref min, ref max);
            IncludeMa(ma20, i, ref min, ref max);
        }
        if (min > max) { min = 0; max = 1; }
        double pad = (max - min) * 0.06;
        if (pad <= 0) pad = Math.Max(0.01, Math.Abs(max) * 0.001);
        min -= pad;
        max += pad;

        List<double> ticks = ChartScale.NiceTicks(min, max, 5);
        using (var gridPen = new Pen(ColorGrid, 1f))
        using (var axisBrush = new SolidBrush(ColorAxis))
        {
            for (int i = 0; i < ticks.Count; i++)
            {
                double t = ticks[i];
                int y = Round(PriceToY(t, min, max, pricePlot));
                g.DrawLine(gridPen, pricePlot.Left, y, pricePlot.Right, y);
                string label = FormatPrice(t);
                SizeF sz = g.MeasureString(label, _axisFont);
                g.DrawString(label, _axisFont, axisBrush,
                    pricePlot.Left - sz.Width - 4 * _scale, y - sz.Height / 2f);
            }

            for (int k = 0; k < 5; k++)
            {
                int idx = start + Round((visible - 1) * k / 4.0);
                if (idx >= s.Bars.Count) idx = s.Bars.Count - 1;
                float x = BarX(idx, start, visible, plot);
                string label = FormatKlineDate(s.Bars[idx].Date);
                SizeF sz = g.MeasureString(label, _axisFont);
                float tx = x - sz.Width / 2f;
                if (tx < plot.Left) tx = plot.Left;
                if (tx + sz.Width > plot.Right) tx = plot.Right - sz.Width;
                g.DrawString(label, _axisFont, axisBrush, tx, plot.Bottom + 4 * _scale);
            }
        }

        double limitRatio = _mode == ChartMode.Daily
            ? ChartScale.LimitRatio(s.Symbol, s.Quote != null ? s.Quote.Name : "") : 0;
        bool[] limitFlags = new bool[s.Bars.Count];
        for (int i = 0; i < s.Bars.Count; i++)
            limitFlags[i] = i > 0 && ChartScale.IsLimitUp(s.Bars[i], s.Bars[i - 1], limitRatio);

        float bodyWidth = Math.Max(2f, plot.Width / (float)visible * 0.62f);
        using (var upBrush = new SolidBrush(ColorUp))
        using (var downBrush = new SolidBrush(ColorDown))
        using (var flatBrush = new SolidBrush(ColorFlat))
        using (var limitFill = new SolidBrush(ColorLimitUp))
        {
            for (int i = start; i < s.Bars.Count; i++)
            {
                DailyBar b = s.Bars[i];
                bool limitUp = limitFlags[i];
                float x = BarX(i, start, visible, plot);
                float yHigh = PriceToY(b.High, min, max, pricePlot);
                float yLow = PriceToY(b.Low, min, max, pricePlot);
                float yOpen = PriceToY(b.Open, min, max, pricePlot);
                float yClose = PriceToY(b.Close, min, max, pricePlot);
                Color color = limitUp
                    ? ColorLimitUp
                    : (b.Close > b.Open ? ColorUp : (b.Close < b.Open ? ColorDown : ColorFlat));
                using (var pen = new Pen(color, Math.Max(1f, _scale)))
                    g.DrawLine(pen, x, yHigh, x, yLow);
                float top = Math.Min(yOpen, yClose);
                float height = Math.Max(1f, Math.Abs(yClose - yOpen));
                if (limitUp)
                {
                    if (height < 2f) height = 2f;
                    g.FillRectangle(limitFill, x - bodyWidth / 2f, top, bodyWidth, height);
                    using (var pen = new Pen(Shade(ColorLimitUp, 1.25f), Math.Max(1.5f, 2f * _scale)))
                        g.DrawRectangle(pen, x - bodyWidth / 2f, top, bodyWidth, height);
                }
                else
                {
                    SolidBrush brush = b.Close > b.Open ? upBrush : (b.Close < b.Open ? downBrush : flatBrush);
                    g.FillRectangle(brush, x - bodyWidth / 2f, top, bodyWidth, height);
                }
            }
        }

        DrawVolume(g, s, plot, volumePlot, start, visible, limitFlags, bodyWidth, maxVolume);

        DrawMa(g, s, ma5, start, visible, pricePlot, min, max, ColorMa5);
        DrawMa(g, s, ma10, start, visible, pricePlot, min, max, ColorMa10);
        DrawMa(g, s, ma20, start, visible, pricePlot, min, max, ColorMa20);

        using (var marker = new SolidBrush(ColorLimitMarker))
        {
            for (int i = start; i < s.Bars.Count; i++)
            {
                if (!limitFlags[i]) continue;
                float x = BarX(i, start, visible, plot);
                float markerY = PriceToY(s.Bars[i].High, min, max, pricePlot) - 7 * _scale;
                if (markerY < pricePlot.Top + 5 * _scale) markerY = pricePlot.Top + 5 * _scale;
                g.FillPolygon(marker, new[]
                {
                    new PointF(x, markerY - 4 * _scale),
                    new PointF(x - 3.5f * _scale, markerY),
                    new PointF(x + 3.5f * _scale, markerY)
                });
            }
        }

        using (var brush5 = new SolidBrush(ColorMa5))
        using (var brush10 = new SolidBrush(ColorMa10))
        using (var brush20 = new SolidBrush(ColorMa20))
        {
            float x = pricePlot.Left + 6 * _scale;
            float y = pricePlot.Top + 2 * _scale;
            g.DrawString("MA5", _legendFont, brush5, x, y);
            x += g.MeasureString("MA5 ", _legendFont).Width;
            g.DrawString("MA10", _legendFont, brush10, x, y);
            x += g.MeasureString("MA10 ", _legendFont).Width;
            g.DrawString("MA20", _legendFont, brush20, x, y);
        }

        if (_mouseIn && plot.Contains(_mouse))
            PaintDailyCrosshair(g, s, ma5, ma10, ma20, plot, pricePlot, volumePlot,
                start, visible, min, max, maxVolume, limitRatio);
    }

    private void PaintDailyCrosshair(Graphics g, DailySeries s, double[] ma5, double[] ma10,
        double[] ma20, Rectangle plot, Rectangle pricePlot, Rectangle volumePlot,
        int start, int visible, double min, double max, double maxVolume, double limitRatio)
    {
        int idx = start + (int)Math.Floor((_mouse.X - plot.Left) / (float)plot.Width * visible);
        if (idx < start) idx = start;
        if (idx >= s.Bars.Count) idx = s.Bars.Count - 1;
        DailyBar b = s.Bars[idx];
        float x = BarX(idx, start, visible, plot);
        float y = PriceToY(b.Close, min, max, pricePlot);
        Rectangle cross = Rectangle.FromLTRB(plot.Left, pricePlot.Top, plot.Right, volumePlot.Bottom);
        if (_mouse.Y > pricePlot.Bottom && _mouse.Y <= volumePlot.Bottom)
        {
            float volumeY = VolumeToY(b.Volume, maxVolume, volumePlot);
            DrawCrossLines(g, cross, volumePlot, x, volumeY);
        }
        else
        {
            DrawCrossLines(g, cross, pricePlot, x, y);
        }

        var lines = new List<string>();
        lines.Add(b.Date);
        lines.Add("开 " + FormatPrice(b.Open) + "  高 " + FormatPrice(b.High));
        lines.Add("低 " + FormatPrice(b.Low) + "  收 " + FormatPrice(b.Close));
        double prevClose = idx > 0 ? s.Bars[idx - 1].Close : b.Open;
        bool limitUp = idx > 0 && ChartScale.IsLimitUp(b, s.Bars[idx - 1], limitRatio);
        lines.Add("涨跌 " + FormatPct(ChartScale.PercentFromPrev(b.Close, prevClose))
            + (limitUp ? "  涨停" : ""));
        lines.Add("量 " + ChartScale.FormatVolume(b.Volume) + "手");
        if (!double.IsNaN(ma5[idx])) lines.Add("MA5 " + FormatPrice(ma5[idx]));
        if (!double.IsNaN(ma10[idx])) lines.Add("MA10 " + FormatPrice(ma10[idx]));
        if (!double.IsNaN(ma20[idx])) lines.Add("MA20 " + FormatPrice(ma20[idx]));
        DrawReadout(g, lines, plot, new PointF(x, y));
    }

    private void DrawVolume(Graphics g, DailySeries s, Rectangle plot, Rectangle volumePlot,
        int start, int visible, bool[] limitFlags, float bodyWidth, double maxVolume)
    {
        DrawVolumeGrid(g, volumePlot, maxVolume);

        using (var upBrush = new SolidBrush(ColorUp))
        using (var downBrush = new SolidBrush(ColorDown))
        using (var flatBrush = new SolidBrush(ColorFlat))
        using (var limitBrush = new SolidBrush(ColorLimitUp))
        {
            for (int i = start; i < s.Bars.Count; i++)
            {
                DailyBar b = s.Bars[i];
                if (b.Volume <= 0) continue;
                float x = BarX(i, start, visible, plot);
                float y = VolumeToY(b.Volume, maxVolume, volumePlot);
                float height = volumePlot.Bottom - y;
                if (height < 1f) height = 1f;
                SolidBrush brush = limitFlags[i]
                    ? limitBrush
                    : (b.Close > b.Open ? upBrush : (b.Close < b.Open ? downBrush : flatBrush));
                g.FillRectangle(brush, x - bodyWidth / 2f, y, bodyWidth, height);
            }
        }

        DrawVolumeLabel(g, volumePlot);
    }

    private void DrawMinuteVolume(Graphics g, Rectangle plot, Rectangle volumePlot,
        double[] volumes, int[] trends, double maxVolume)
    {
        DrawVolumeGrid(g, volumePlot, maxVolume);

        float barWidth = Math.Max(1f, plot.Width / 240f * 0.65f);
        float half = barWidth / 2f;
        using (var upBrush = new SolidBrush(ColorUp))
        using (var downBrush = new SolidBrush(ColorDown))
        using (var flatBrush = new SolidBrush(ColorFlat))
        {
            for (int i = 0; i < volumes.Length; i++)
            {
                double volume = volumes[i];
                if (volume <= 0) continue;
                float x = MinuteX(i, plot);
                if (x - half < plot.Left) x = plot.Left + half;
                if (x + half > plot.Right) x = plot.Right - half;
                float y = VolumeToY(volume, maxVolume, volumePlot);
                float height = volumePlot.Bottom - y;
                if (height < 1f) height = 1f;
                SolidBrush brush = trends[i] > 0 ? upBrush : (trends[i] < 0 ? downBrush : flatBrush);
                g.FillRectangle(brush, x - half, y, barWidth, height);
            }
        }

        DrawVolumeLabel(g, volumePlot);
    }

    private void DrawVolumeGrid(Graphics g, Rectangle volumePlot, double maxVolume)
    {
        using (var basePen = new Pen(ColorGrid, 1f))
            g.DrawLine(basePen, volumePlot.Left, volumePlot.Bottom, volumePlot.Right, volumePlot.Bottom);

        if (maxVolume <= 0) return;
        List<double> ticks = ChartScale.NiceTicks(0, maxVolume, 2);
        using (var gridPen = new Pen(ColorGrid, 1f))
        using (var axisBrush = new SolidBrush(ColorAxis))
        {
            for (int i = 0; i < ticks.Count; i++)
            {
                double t = ticks[i];
                int y = Round(VolumeToY(t, maxVolume, volumePlot));
                g.DrawLine(gridPen, volumePlot.Left, y, volumePlot.Right, y);
                string label = ChartScale.FormatVolume(t);
                SizeF sz = g.MeasureString(label, _axisFont);
                g.DrawString(label, _axisFont, axisBrush,
                    volumePlot.Left - sz.Width - 4 * _scale, y - sz.Height / 2f);
            }
        }
    }

    private void DrawVolumeLabel(Graphics g, Rectangle volumePlot)
    {
        string volumeLabel = "成交量";
        float labelX = volumePlot.Left + 6 * _scale;
        float labelY = volumePlot.Top + 1 * _scale;
        SizeF labelSize = g.MeasureString(volumeLabel, _legendFont);
        using (var labelBg = new SolidBrush(Shade(_bg, 1.2f)))
            g.FillRectangle(labelBg, labelX - 2 * _scale, labelY,
                labelSize.Width + 4 * _scale, labelSize.Height);
        using (var axisBrush = new SolidBrush(ColorAxis))
            g.DrawString(volumeLabel, _legendFont, axisBrush, labelX, labelY);
    }

    private void DrawMa(Graphics g, DailySeries s, double[] ma, int start, int visible,
        Rectangle plot, double min, double max, Color color)
    {
        var points = new List<PointF>();
        for (int i = start; i < s.Bars.Count; i++)
        {
            double v = ma[i];
            if (double.IsNaN(v)) continue;
            points.Add(new PointF(BarX(i, start, visible, plot), PriceToY(v, min, max, plot)));
        }
        DrawPolyline(g, points, color, 1.1f * _scale);
    }

    private static void IncludeMa(double[] ma, int i, ref double min, ref double max)
    {
        double v = ma[i];
        if (double.IsNaN(v)) return;
        if (v < min) min = v;
        if (v > max) max = v;
    }

    private void DrawPolyline(Graphics g, List<PointF> points, Color color, float width)
    {
        if (points.Count == 0) return;
        using (var pen = new Pen(color, Math.Max(1f, width)))
        {
            pen.LineJoin = LineJoin.Round;
            if (points.Count == 1)
                g.DrawEllipse(pen, points[0].X - 1, points[0].Y - 1, 2, 2);
            else
                g.DrawLines(pen, points.ToArray());
        }
    }

    private void DrawCrossLines(Graphics g, Rectangle vertical, Rectangle horizontal, float x, float y)
    {
        using (var pen = new Pen(ColorCross, 1f))
        {
            pen.DashStyle = DashStyle.Dash;
            g.DrawLine(pen, x, vertical.Top, x, vertical.Bottom);
            g.DrawLine(pen, horizontal.Left, y, horizontal.Right, y);
        }
    }

    private void DrawReadout(Graphics g, List<string> lines, Rectangle plot, PointF anchor)
    {
        if (lines.Count == 0) return;
        float pad = 6 * _scale;
        float lineHeight = _readoutFont.GetHeight(g);
        float width = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            SizeF sz = g.MeasureString(lines[i], _readoutFont);
            if (sz.Width > width) width = sz.Width;
        }
        float boxWidth = width + pad * 2;
        float boxHeight = lineHeight * lines.Count + pad * 2;
        float x = anchor.X + 14 * _scale;
        float y = anchor.Y - boxHeight - 10 * _scale;
        if (x + boxWidth > plot.Right) x = anchor.X - boxWidth - 14 * _scale;
        if (y < plot.Top) y = anchor.Y + 14 * _scale;
        if (x < plot.Left) x = plot.Left;
        if (y + boxHeight > plot.Bottom) y = plot.Bottom - boxHeight;

        using (var fill = new SolidBrush(Shade(_bg, 1.35f)))
        using (var border = new Pen(ColorGrid))
        {
            g.FillRectangle(fill, x, y, boxWidth, boxHeight);
            g.DrawRectangle(border, x, y, boxWidth, boxHeight);
        }
        using (var brush = new SolidBrush(_text))
        {
            for (int i = 0; i < lines.Count; i++)
                g.DrawString(lines[i], _readoutFont, brush, x + pad, y + pad + i * lineHeight);
        }
    }

    private void DrawCenterMessage(Graphics g, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        using (var brush = new SolidBrush(color))
        {
            SizeF sz = g.MeasureString(text, _messageFont);
            g.DrawString(text, _messageFont, brush,
                (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
        }
    }

    private float MinuteX(int minuteIndex, Rectangle plot)
    {
        return plot.Left + (minuteIndex / 240f) * plot.Width;
    }

    private static float BarX(int index, int start, int visible, Rectangle plot)
    {
        return plot.Left + (index - start + 0.5f) * (plot.Width / (float)visible);
    }

    private static float PriceToY(double price, double min, double max, Rectangle plot)
    {
        double range = max - min;
        if (range <= 0) return plot.Top + plot.Height / 2f;
        return (float)(plot.Bottom - (price - min) / range * plot.Height);
    }

    private static float VolumeToY(double volume, double maxVolume, Rectangle plot)
    {
        if (maxVolume <= 0) return plot.Bottom;
        double ratio = volume / maxVolume;
        if (ratio < 0) ratio = 0;
        if (ratio > 1) ratio = 1;
        return (float)(plot.Bottom - ratio * plot.Height);
    }

    private string FormatKlineDate(string date)
    {
        if (_mode == ChartMode.Monthly) return ChartScale.FormatMonth(date);
        if (_mode == ChartMode.Weekly) return ChartScale.FormatWeek(date);
        return ChartScale.FormatShortDate(date);
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

    private static string FormatTime(string hhmm)
    {
        if (string.IsNullOrEmpty(hhmm) || hhmm.Length != 4) return hhmm ?? "";
        return hhmm.Substring(0, 2) + ":" + hhmm.Substring(2, 2);
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

    private static int Round(double value)
    {
        return (int)Math.Round(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _axisFont.Dispose();
            _legendFont.Dispose();
            _readoutFont.Dispose();
            _messageFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
