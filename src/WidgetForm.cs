using System;
using System.Drawing;
using System.Windows.Forms;

public sealed class WidgetForm : Form
{
    private readonly bool _attachDesktopLayer;
    private readonly Point _homeLocation;

    public WidgetForm(bool attachDesktopLayer)
    {
        _attachDesktopLayer = attachDesktopLayer;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 36);
        Size = new Size(190, 110);
        _homeLocation = new Point(
            Screen.PrimaryScreen.WorkingArea.Right - Width - 24,
            Screen.PrimaryScreen.WorkingArea.Top + 24);
        Location = _homeLocation;

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
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_attachDesktopLayer && DesktopLayer.Attach(Handle))
        {
            Location = _homeLocation;
            Size = new Size(190, 110);
        }
    }
}
