using System;
using System.Drawing;
using System.IO;

public static class PosConfig
{
    public static string DefaultPath
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StockWidget", "pos.ini");
        }
    }

    public static Point Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return Point.Empty;
            int x = int.MinValue, y = int.MinValue;
            foreach (string line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (t.StartsWith("X="))
                {
                    int v;
                    if (int.TryParse(t.Substring(2), out v)) x = v;
                }
                else if (t.StartsWith("Y="))
                {
                    int v;
                    if (int.TryParse(t.Substring(2), out v)) y = v;
                }
            }
            if (x == int.MinValue || y == int.MinValue) return Point.Empty;
            return new Point(x, y);
        }
        catch
        {
            return Point.Empty;
        }
    }

    public static void Save(string path, Point p)
    {
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(path, new[] { "X=" + p.X, "Y=" + p.Y });
        }
        catch { }
    }

    public static Point Clamp(Point p, Rectangle area)
    {
        int x = Math.Max(area.Left, Math.Min(p.X, area.Right - 1));
        int y = Math.Max(area.Top, Math.Min(p.Y, area.Bottom - 1));
        return new Point(x, y);
    }
}
