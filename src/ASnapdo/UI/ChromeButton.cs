using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ASnapdo.UI;

/// <summary>A compact, keyboard-accessible button used by the editor chrome.</summary>
public sealed class ChromeButton : Button
{
    private bool hovered;
    private bool pressed;
    private bool selected;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string IconKey { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IconAboveText { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get => selected; set { if (selected == value) return; selected = value; Invalidate(); } }

    public ChromeButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9f); ForeColor = Color.FromArgb(51, 65, 85);
        AccessibleRole = AccessibleRole.PushButton;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(OpaqueBackground(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var fill = Primary ? (pressed ? Color.FromArgb(29, 78, 216) : hovered ? Color.FromArgb(29, 78, 216) : Color.FromArgb(37, 99, 235))
            : Selected ? Color.FromArgb(231, 240, 255) : pressed ? Color.FromArgb(226, 232, 240) : hovered ? Color.FromArgb(241, 245, 249) : BackColor;
        using var path = Rounded(bounds, Math.Min(8, Math.Min(Width, Height) / 3));
        using var background = new SolidBrush(fill); g.FillPath(background, path);
        if (Selected || Focused && ShowFocusCues)
        {
            using var border = new Pen(Selected ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235)); g.DrawPath(border, path);
        }
        var foreground = !Enabled ? Color.FromArgb(148, 163, 184) : Primary ? Color.White : Selected ? Color.FromArgb(37, 99, 235) : ForeColor;
        bool hasIcon = !string.IsNullOrEmpty(IconKey);
        int iconSize = Math.Min(20, Math.Max(12, Height - 12));
        if (IconAboveText && hasIcon)
        {
            DrawIcon(g, IconKey, new Rectangle((Width - iconSize) / 2, 7, iconSize, iconSize), foreground);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(2, iconSize + 8, Width - 4, Math.Max(0, Height - iconSize - 10)), foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
        else
        {
            int iconX = string.IsNullOrEmpty(Text) ? (Width - iconSize) / 2 : 11;
            if (hasIcon) DrawIcon(g, IconKey, new Rectangle(iconX, (Height - iconSize) / 2, iconSize, iconSize), foreground);
            int left = hasIcon ? iconX + iconSize + 7 : 5;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(left, 0, Math.Max(0, Width - left - 6), Height), foreground,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (hasIcon ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.NoPadding);
        }
    }

    internal static Color OpaqueBackground(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            if (parent.BackColor.A == 255) return parent.BackColor;
        return SystemColors.Control;
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath(); int d = Math.Max(2, radius * 2);
        path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    private static void DrawIcon(Graphics g, string key, Rectangle r, Color color)
    {
        var state = g.Save(); g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 20f, r.Height / 20f);
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        switch (key)
        {
            case "crop": g.DrawLines(pen, [new Point(5, 1), new Point(5, 15), new Point(19, 15)]); g.DrawLines(pen, [new Point(1, 5), new Point(15, 5), new Point(15, 19)]); break;
            case "region-select":
                pen.DashStyle = DashStyle.Dash;
                g.DrawRectangle(pen, 3, 3, 14, 14);
                break;
            case "region-copy":
                pen.DashStyle = DashStyle.Dash;
                g.DrawRectangle(pen, 2, 2, 11, 11);
                pen.DashStyle = DashStyle.Solid;
                g.DrawRectangle(pen, 8, 8, 10, 10);
                break;
            case "capture": g.DrawRectangle(pen, 3, 3, 14, 14); g.DrawLine(pen, 7, 1, 7, 5); g.DrawLine(pen, 1, 7, 5, 7); g.DrawLine(pen, 13, 15, 13, 19); g.DrawLine(pen, 15, 13, 19, 13); break;
            case "monitor": g.DrawRectangle(pen, 2, 3, 16, 11); g.DrawLine(pen, 10, 14, 10, 17); g.DrawLine(pen, 6, 18, 14, 18); break;
            case "select": g.DrawPolygon(pen, [new Point(4, 2), new Point(4, 15), new Point(7, 12), new Point(10, 18), new Point(13, 16), new Point(10, 11), new Point(15, 11)]); break;
            case "arrow": g.DrawLine(pen, 3, 17, 17, 3); g.DrawLines(pen, [new Point(9, 3), new Point(17, 3), new Point(17, 11)]); break;
            case "rectangle": g.DrawRectangle(pen, 3, 4, 14, 12); break;
            case "text": g.DrawLine(pen, 3, 4, 17, 4); g.DrawLine(pen, 10, 4, 10, 17); g.DrawLine(pen, 7, 17, 13, 17); break;
            case "number": g.DrawEllipse(pen, 2, 2, 16, 16); using (var f = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("1", f, brush, 7, 4); break;
            case "highlight": g.DrawPolygon(pen, [new Point(3, 14), new Point(12, 4), new Point(16, 8), new Point(7, 18), new Point(3, 18)]); g.DrawLine(pen, 2, 19, 18, 19); break;
            case "open": g.DrawLines(pen, [new Point(2, 6), new Point(2, 16), new Point(17, 16), new Point(19, 8), new Point(6, 8), new Point(4, 16)]); g.DrawLines(pen, [new Point(2, 6), new Point(7, 6), new Point(9, 8)]); break;
            case "save": g.DrawRectangle(pen, 3, 2, 14, 16); g.DrawRectangle(pen, 6, 2, 8, 5); g.DrawRectangle(pen, 6, 11, 8, 7); break;
            case "copy": g.DrawRectangle(pen, 6, 5, 11, 13); g.DrawLines(pen, [new Point(3, 14), new Point(3, 2), new Point(13, 2)]); break;
            case "undo": g.DrawArc(pen, 4, 5, 12, 12, 180, 190); g.DrawLines(pen, [new Point(4, 10), new Point(4, 5), new Point(9, 5)]); break;
            case "minus": g.DrawLine(pen, 4, 10, 16, 10); break;
            case "plus": g.DrawLine(pen, 4, 10, 16, 10); g.DrawLine(pen, 10, 4, 10, 16); break;
            case "fit": g.DrawLines(pen, [new Point(3, 7), new Point(3, 3), new Point(7, 3)]); g.DrawLines(pen, [new Point(13, 3), new Point(17, 3), new Point(17, 7)]); g.DrawLines(pen, [new Point(17, 13), new Point(17, 17), new Point(13, 17)]); g.DrawLines(pen, [new Point(7, 17), new Point(3, 17), new Point(3, 13)]); break;
            case "settings": g.DrawEllipse(pen, 3, 3, 14, 14); g.DrawEllipse(pen, 7, 7, 6, 6); g.DrawLine(pen, 10, 1, 10, 3); g.DrawLine(pen, 10, 17, 10, 19); g.DrawLine(pen, 1, 10, 3, 10); g.DrawLine(pen, 17, 10, 19, 10); break;
            case "help": g.DrawEllipse(pen, 2, 2, 16, 16); g.DrawArc(pen, 7, 5, 6, 6, 180, 240); g.DrawLine(pen, 10, 10, 10, 12); g.DrawEllipse(pen, 9.5f, 14.5f, 1, 1); break;
            case "paste": g.DrawLines(pen, [new Point(7, 4), new Point(4, 4), new Point(4, 18), new Point(16, 18), new Point(16, 4), new Point(13, 4)]); g.DrawRectangle(pen, 7, 2, 6, 4); g.DrawLine(pen, 7, 10, 13, 10); g.DrawLine(pen, 7, 14, 12, 14); break;
            case "delete": g.DrawLine(pen, 3, 5, 17, 5); g.DrawLines(pen, [new Point(5, 5), new Point(6, 18), new Point(14, 18), new Point(15, 5)]); g.DrawLines(pen, [new Point(7, 5), new Point(7, 2), new Point(13, 2), new Point(13, 5)]); g.DrawLine(pen, 8, 8, 8, 14); g.DrawLine(pen, 12, 8, 12, 14); break;
            case "check": g.DrawLines(pen, [new Point(3, 10), new Point(8, 15), new Point(17, 5)]); break;
            case "close": g.DrawLine(pen, 5, 5, 15, 15); g.DrawLine(pen, 15, 5, 5, 15); break;
            case "mosaic": for (int y = 3; y < 18; y += 5) for (int x = 3; x < 18; x += 5) { if ((x + y) % 2 == 0) g.FillRectangle(brush, x, y, 4, 4); else g.DrawRectangle(pen, x, y, 3, 3); } break;
            case "blur": g.DrawEllipse(pen, 3, 3, 14, 14); using (var soft = new Pen(Color.FromArgb(100, color), 3)) g.DrawEllipse(soft, 6, 6, 8, 8); break;
        }
        g.Restore(state);
    }
}

/// <summary>Small original mark drawn at any display scale.</summary>
public sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleName = "ASnapdo"; AccessibleRole = AccessibleRole.Graphic;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Draw(e.Graphics, Size);
    }
    public static void Draw(Graphics g, Size size)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float sx = size.Width / 40f, sy = size.Height / 40f;
        g.ScaleTransform(sx, sy);
        using var path = new GraphicsPath();
        path.AddArc(0, 0, 14, 14, 180, 90); path.AddArc(26, 0, 14, 14, 270, 90);
        path.AddArc(26, 26, 14, 14, 0, 90); path.AddArc(0, 26, 14, 14, 90, 90); path.CloseFigure();
        using var background = new SolidBrush(Color.FromArgb(37, 99, 235)); g.FillPath(background, path);
        using var pen = new Pen(Color.White, 2.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(pen, [new Point(10, 16), new Point(10, 11), new Point(15, 11)]);
        g.DrawLines(pen, [new Point(25, 11), new Point(30, 11), new Point(30, 16)]);
        g.DrawLines(pen, [new Point(30, 24), new Point(30, 29), new Point(25, 29)]);
        g.DrawLines(pen, [new Point(15, 29), new Point(10, 29), new Point(10, 24)]);
        g.DrawEllipse(pen, 16, 16, 8, 8);
        g.Restore(state);
    }
}

public sealed class ColorSwatch : Button
{
    private bool selected;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SwatchColor { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get => selected; set { if (selected == value) return; selected = value; Invalidate(); } }
    public ColorSwatch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.PushButton;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(ChromeButton.OpaqueBackground(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
        int size = Math.Min(Width, Height); var outer = new Rectangle((Width - size) / 2 + 1, (Height - size) / 2 + 1, size - 3, size - 3);
        if (Selected || Focused && ShowFocusCues)
        {
            using var ring = new Pen(Color.FromArgb(37, 99, 235), 1.5f); g.DrawEllipse(ring, outer);
        }
        outer.Inflate(-4, -4);
        using var fill = new SolidBrush(SwatchColor); g.FillEllipse(fill, outer);
        using var edge = new Pen(Color.FromArgb(30, 15, 23, 42)); g.DrawEllipse(edge, outer);
    }
}
