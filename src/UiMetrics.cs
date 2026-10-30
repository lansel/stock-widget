using System;
using System.Drawing;

public sealed class UiMetrics
{
    public readonly float Scale;
    public readonly int Width;
    public readonly int RowHeight;
    public readonly int MarginX;
    public readonly int MarginY;
    public readonly int NameColumnWidth;
    public readonly int PriceColumnWidth;
    public readonly int PctColumnWidth;
    public readonly int Radius;
    public readonly int MinHeight;
    public readonly int DefaultMargin;
    public readonly int DotRight;
    public readonly int DotTop;

    private UiMetrics(float scale)
    {
        Scale = scale;
        MarginX = Round(12 * scale);
        MarginY = Round(10 * scale);
        RowHeight = Round(26 * scale);
        NameColumnWidth = Round(96 * scale);
        PriceColumnWidth = Round(56 * scale);
        PctColumnWidth = Round(70 * scale);
        Radius = Round(10 * scale);
        MinHeight = Round(50 * scale);
        DefaultMargin = Round(24 * scale);
        DotRight = Round(16 * scale);
        DotTop = Round(2 * scale);
        Width = MarginX * 2 + NameColumnWidth + PctColumnWidth;
    }

    public static UiMetrics FromSystemDpi()
    {
        float scale;
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            scale = g.DpiX / 96f;
        if (scale <= 0f) scale = 1f;
        return new UiMetrics(scale);
    }

    private static int Round(float value)
    {
        return (int)Math.Round(value);
    }
}
