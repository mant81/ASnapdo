using ASnapdo.Services;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ASnapdo.UI;

public sealed class SelectionForm : Form
{
    private readonly Bitmap desktop;
    private readonly Rectangle desktopBounds;
    private Point start, pointer;
    private Rectangle selection;
    private bool dragging;
    public Rectangle SelectedBounds => new(selection.X + desktopBounds.X, selection.Y + desktopBounds.Y, selection.Width, selection.Height);

    public SelectionForm() : this(SystemInformation.VirtualScreen) { }
    private SelectionForm(Rectangle bounds) : this(CaptureService.Capture(bounds), bounds) { }

    /// <summary>Takes ownership of the supplied unscaled desktop snapshot.</summary>
    public SelectionForm(Bitmap snapshot, Rectangle bounds)
    {
        if (snapshot.Size != bounds.Size) throw new ArgumentException("Snapshot and screen bounds must have the same pixel size.");
        desktop = snapshot; desktopBounds = bounds;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
        Bounds = bounds; BackColor = Color.Black; TopMost = true; Cursor = Cursors.Cross;
        DoubleBuffered = true; KeyPreview = true; ShowInTaskbar = false;
        pointer = Clamp(PointToClient(System.Windows.Forms.Cursor.Position));
    }
    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, desktop.Width), Math.Clamp(p.Y, 0, desktop.Height));
    private void UpdateSelection()
    {
        var end = pointer;
        if ((ModifierKeys & Keys.Shift) != 0)
        {
            int side = Math.Min(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            end = new(start.X + Math.Sign(end.X - start.X) * side, start.Y + Math.Sign(end.Y - start.Y) * side);
        }
        selection = Rectangle.FromLTRB(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right) { CancelSelection(); return; }
        if (e.Button != MouseButtons.Left) return;
        pointer = start = Clamp(e.Location); selection = Rectangle.Empty; dragging = true; Capture = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); pointer = Clamp(e.Location);
        if (dragging) UpdateSelection();
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!dragging || e.Button != MouseButtons.Left) return;
        pointer = Clamp(e.Location); UpdateSelection(); dragging = false; Capture = false;
        if (selection.Width > 1 && selection.Height > 1) DialogResult = DialogResult.OK;
        else { selection = Rectangle.Empty; Invalidate(); }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) { e.Handled = true; CancelSelection(); }
        else if (dragging && e.KeyCode == Keys.ShiftKey) { UpdateSelection(); Invalidate(); }
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (dragging && e.KeyCode == Keys.ShiftKey) { UpdateSelection(); Invalidate(); }
    }
    private void CancelSelection() { dragging = false; Capture = false; DialogResult = DialogResult.Cancel; }
    public Bitmap CaptureSelection()
    {
        if (DialogResult != DialogResult.OK || selection.Width < 2 || selection.Height < 2) throw new InvalidOperationException("No completed selection.");
        return desktop.Clone(selection, PixelFormat.Format32bppArgb);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.DrawImageUnscaled(desktop, 0, 0);
        using (var shade = new SolidBrush(Color.FromArgb(115, 0, 0, 0))) g.FillRectangle(shade, ClientRectangle);
        if (selection.Width > 0 && selection.Height > 0)
            g.DrawImage(desktop, selection, selection, GraphicsUnit.Pixel);
        using var guide = new Pen(Color.FromArgb(200, Color.White)) { DashStyle = DashStyle.Dash };
        g.DrawLine(guide, pointer.X, 0, pointer.X, Height);
        g.DrawLine(guide, 0, pointer.Y, Width, pointer.Y);
        if (selection.Width > 0 && selection.Height > 0)
        {
            g.DrawLine(guide, selection.Left, 0, selection.Left, Height);
            g.DrawLine(guide, selection.Right, 0, selection.Right, Height);
            g.DrawLine(guide, 0, selection.Top, Width, selection.Top);
            g.DrawLine(guide, 0, selection.Bottom, Width, selection.Bottom);
            using var border = new Pen(Color.FromArgb(37, 99, 235), 2);
            g.DrawRectangle(border, selection);
        }
        DrawHud(g);
    }
    private void DrawHud(Graphics g)
    {
        float scale = DeviceDpi / 96f;
        int pad = (int)(12 * scale), width = (int)(290 * scale), height = (int)(170 * scale);
        var monitor = Screen.FromPoint(new Point(desktopBounds.X + pointer.X, desktopBounds.Y + pointer.Y)).Bounds;
        monitor.Offset(-desktopBounds.X, -desktopBounds.Y);
        var available = Rectangle.Intersect(ClientRectangle, monitor);
        if (available.IsEmpty) available = ClientRectangle;
        width = Math.Min(width, available.Width); height = Math.Min(height, available.Height);
        int x = pointer.X + 24, y = pointer.Y + 24;
        if (x + width > available.Right) x = pointer.X - width - 24;
        if (y + height > available.Bottom) y = pointer.Y - height - 24;
        x = Math.Clamp(x, available.Left, available.Right - width); y = Math.Clamp(y, available.Top, available.Bottom - height);
        if (dragging && selection.Width > 0 && selection.Height > 0)
        {
            var candidates = new[]
            {
                new Point(x, y), new Point(pointer.X - width - 24, pointer.Y + 24),
                new Point(pointer.X + 24, pointer.Y - height - 24), new Point(pointer.X - width - 24, pointer.Y - height - 24),
                new Point(selection.Right + 12, pointer.Y), new Point(selection.Left - width - 12, pointer.Y),
                new Point(pointer.X, selection.Bottom + 12), new Point(pointer.X, selection.Top - height - 12)
            }.Select(p => new Point(Math.Clamp(p.X, available.Left, available.Right - width), Math.Clamp(p.Y, available.Top, available.Bottom - height)));
            var best = candidates.OrderBy(p =>
            {
                var overlap = Rectangle.Intersect(selection, new Rectangle(p, new Size(width, height)));
                return (long)overlap.Width * overlap.Height;
            }).ThenBy(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y)).First();
            x = best.X; y = best.Y;
        }
        using var background = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.FillRectangle(background, x, y, width, height);
        using var body = new Font("Segoe UI", 9, FontStyle.Regular);
        using var title = new Font("Segoe UI", 12, FontStyle.Bold);
        var location = dragging ? SelectedBounds.Location : new Point(desktopBounds.X + pointer.X, desktopBounds.Y + pointer.Y);
        string size = dragging ? $"{selection.Width} × {selection.Height} px" : "영역을 드래그하세요";
        g.DrawString(size, title, Brushes.White, x + pad, y + pad);
        g.DrawString($"X {location.X}   Y {location.Y}", body, Brushes.LightSteelBlue, x + pad, y + pad + 28 * scale);
        g.DrawString("Shift 정사각형 · Esc / 우클릭 취소", body, Brushes.White, x + pad, y + height - 28 * scale);
        // Magnify the original pixels only; guides must never enter the captured image.
        const int cells = 17;
        int cell = Math.Max(3, (int)(4 * scale));
        int preview = cells * cell, px = x + pad, py = y + (int)(66 * scale);
        int sx = Math.Clamp(pointer.X, 0, desktop.Width - 1), sy = Math.Clamp(pointer.Y, 0, desktop.Height - 1);
        var state = g.Save();
        g.SetClip(new Rectangle(px, py, preview, preview));
        g.FillRectangle(Brushes.Black, px, py, preview, preview);
        var source = Rectangle.Intersect(new Rectangle(sx - cells / 2, sy - cells / 2, cells, cells), new Rectangle(Point.Empty, desktop.Size));
        var destination = new Rectangle(px + (source.X - (sx - cells / 2)) * cell, py + (source.Y - (sy - cells / 2)) * cell, source.Width * cell, source.Height * cell);
        g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(desktop, destination, source, GraphicsUnit.Pixel); g.Restore(state);
        using var marker = new Pen(Color.FromArgb(96, 165, 250));
        g.DrawRectangle(marker, px + cells / 2 * cell, py + cells / 2 * cell, cell, cell);
        g.DrawRectangle(Pens.SlateGray, px, py, preview, preview);
        var color = desktop.GetPixel(sx, sy);
        g.DrawString($"{cell}× 확대\n#{color.R:X2}{color.G:X2}{color.B:X2}", body, Brushes.LightSteelBlue, px + preview + pad, py + 8);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) desktop.Dispose();
        base.Dispose(disposing);
    }
}
