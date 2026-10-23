using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PxBrowser;

internal class PxDialog : Form
{
    public Panel Body { get; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public PxDialog(string title, int width, int height)
    {
        Text = title;
        Width = width;
        Height = height;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        KeyPreview = true;
        BackColor = Theme.Ink;
        ForeColor = Theme.Cream;
        Font = new Font("Yu Gothic UI", 9F);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Theme.Ink,
        };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold),
            ForeColor = Theme.Cream,
            BackColor = Theme.Ink,
        };
        var close = new Panel
        {
            Dock = DockStyle.Right,
            Width = 48,
            BackColor = Theme.Ink,
            Cursor = Cursors.Hand,
        };
        close.Paint += (_, e) =>
        {
            e.Graphics.Clear(close.BackColor);
            TextRenderer.DrawText(e.Graphics, "\uE8BB",
                new Font("Segoe Fluent Icons", 10F), close.ClientRectangle,
                Theme.Cream, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        close.MouseEnter += (_, _) => { close.BackColor = Color.FromArgb(196, 43, 28); close.Invalidate(); };
        close.MouseLeave += (_, _) => { close.BackColor = Theme.Ink; close.Invalidate(); };
        close.Click += (_, _) => Close();

        var line = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Line };
        header.Controls.Add(titleLabel);
        header.Controls.Add(close);
        header.Controls.Add(line);
        line.BringToFront();

        Body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Ink,
            ForeColor = Theme.Cream,
        };

        Controls.Add(Body);
        Controls.Add(header);
        header.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            var dark = Theme.IsLight ? 0 : 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            var corner = 2;
            DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
            var border = Theme.Ink.R | (Theme.Ink.G << 8) | (Theme.Ink.B << 16);
            DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
        }
        catch { }
    }
}

internal sealed class PxListView : ListView
{
    public PxListView()
    {
        View = View.Details;
        FullRowSelect = true;
        OwnerDraw = true;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        BackColor = Theme.Ink;
        ForeColor = Theme.Cream;
        Font = new Font("Yu Gothic UI", 9F);
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using var background = new SolidBrush(Theme.InkSoft);
        e.Graphics.FillRectangle(background, e.Bounds);
        using var line = new Pen(Theme.Line);
        e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", Font, e.Bounds,
            Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        StretchLastColumn();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        StretchLastColumn();
    }

    private void StretchLastColumn()
    {
        if (Columns.Count == 0 || ClientSize.Width <= 0) return;
        var used = 0;
        for (var i = 0; i < Columns.Count - 1; i++) used += Columns[i].Width;
        var width = ClientSize.Width - used;
        if (width > 120 && Columns[^1].Width != width) Columns[^1].Width = width;
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        using var background = new SolidBrush(e.Item.Selected ? Theme.InkSoft : Theme.Ink);
        e.Graphics.FillRectangle(background, e.Bounds);
        e.DrawDefault = false;
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var bounds = e.Bounds;
        bounds.Inflate(-6, 0);
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", Font, bounds,
            Theme.Cream, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class PxCheckBox : CheckBox
{
    public PxCheckBox()
    {
        AutoSize = false;
        Height = 24;
        BackColor = Theme.Ink;
        ForeColor = Theme.Cream;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Ink);
        var box = new Rectangle(1, (Height - 16) / 2, 16, 16);
        using var fill = new SolidBrush(Checked ? Theme.InkSoft : Theme.Ink);
        using var border = new Pen(Checked ? Theme.Gold : Theme.Line);
        using var path = RoundedRect(box, 4);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        if (Checked)
        {
            TextRenderer.DrawText(e.Graphics, "\uE73E", new Font("Segoe Fluent Icons", 8F),
                box, Theme.Gold, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        var text = new Rectangle(24, 0, Math.Max(0, Width - 24), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Theme.Cream,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class PxRadio : RadioButton
{
    public PxRadio()
    {
        AutoSize = false;
        Height = 24;
        BackColor = Theme.Ink;
        ForeColor = Theme.Cream;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Ink);
        var circle = new Rectangle(2, (Height - 16) / 2, 16, 16);
        using var fill = new SolidBrush(Checked ? Theme.InkSoft : Theme.Ink);
        using var border = new Pen(Checked ? Theme.Gold : Theme.Line);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(fill, circle);
        e.Graphics.DrawEllipse(border, circle);
        if (Checked)
        {
            using var dot = new SolidBrush(Theme.Gold);
            e.Graphics.FillEllipse(dot, new Rectangle(circle.Left + 6, circle.Top + 6, 4, 4));
        }
        var text = new Rectangle(26, 0, Math.Max(0, Width - 26), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Theme.Cream,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}
internal static class PxMessageBox
{
    public static bool Confirm(IWin32Window owner, string title, string message)
    {
        using var dialog = new PxDialog(title, 500, 250);
        var label = new Label
        {
            Text = message,
            Left = 20,
            Top = 22,
            Width = 440,
            Height = 90,
            ForeColor = Theme.Cream,
        };
        var yes = new Button { Text = "はい", Left = 260, Top = 165, Width = 90, DialogResult = DialogResult.OK };
        var no = new Button { Text = "いいえ", Left = 360, Top = 165, Width = 90, DialogResult = DialogResult.Cancel };
        StyleButton(yes);
        StyleButton(no);
        dialog.Body.Controls.AddRange(new Control[] { label, yes, no });
        dialog.AcceptButton = yes;
        dialog.CancelButton = no;
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static void Info(IWin32Window owner, string title, string message)
    {
        using var dialog = new PxDialog(title, 520, 280);
        var label = new Label
        {
            Text = message,
            Left = 20,
            Top = 22,
            Width = 460,
            Height = 130,
            ForeColor = Theme.Cream,
        };
        var ok = new Button { Text = "OK", Left = 390, Top = 195, Width = 90, DialogResult = DialogResult.OK };
        StyleButton(ok);
        dialog.Body.Controls.Add(label);
        dialog.Body.Controls.Add(ok);
        dialog.AcceptButton = ok;
        dialog.CancelButton = ok;
        dialog.ShowDialog(owner);
    }

    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Theme.InkSoft;
        button.ForeColor = Theme.Cream;
        button.FlatAppearance.BorderColor = Theme.Gold;
    }
}