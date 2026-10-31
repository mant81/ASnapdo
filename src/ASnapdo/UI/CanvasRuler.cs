using System.Drawing.Drawing2D;

namespace ASnapdo.UI;

// A viewport overlay: coordinates always refer to image pixels, not screen pixels.
public sealed class CanvasRuler : Control
{
    private readonly EditorCanvas editor;
    public bool Vertical { get; }
    public float ImageOrigin => Vertical
        ? PointToClient(editor.PointToScreen(Point.Empty)).Y
        : PointToClient(editor.PointToScreen(Point.Empty)).X;
    public float PositionForPixel(float pixel) => ImageOrigin + pixel * editor.Document.Zoom;

    public CanvasRuler(EditorCanvas editor, bool vertical)
    {
        this.editor = editor; Vertical = vertical;
        DoubleBuffered = true; TabStop = false;
        BackColor = Color.FromArgb(248, 250, 252); ForeColor = Color.FromArgb(100, 116, 139);
        AccessibleName = vertical ? "세로 줄자 (픽셀)" : "가로 줄자 (픽셀)";
        Dock = vertical ? DockStyle.Left : DockStyle.Top;
        Width = Height = 26;
        editor.LocationChanged += (_, _) => Invalidate();
        editor.SizeChanged += (_, _) => Invalidate();
        editor.Invalidated += (_, _) => Invalidate();
        if (editor.Parent is ScrollableControl viewport)
        {
            viewport.Scroll += (_, _) => Invalidate();
            viewport.Resize += (_, _) => Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!editor.IsHandleCreated || editor.IsDisposed) return;
        float zoom = editor.Document.Zoom, origin = ImageOrigin;
        int length = Vertical ? Height : Width;
        int thickness = Vertical ? Width : Height;
        int imageLength = Vertical ? editor.Document.Image.Height : editor.Document.Image.Width;
        float desired = 65f * DeviceDpi / 96 / zoom;
        float power = MathF.Pow(10, MathF.Floor(MathF.Log10(desired)));
        float major = new[] { 1f, 2f, 5f, 10f }.First(n => n * power >= desired) * power;
        float step = major / 5;
        int first = Math.Max(0, (int)MathF.Ceiling(-origin / zoom / step));
        int last = (int)MathF.Floor(Math.Min(imageLength, (length - origin) / zoom) / step);
        using var pen = new Pen(Color.FromArgb(148, 163, 184));
        using var border = new Pen(Color.FromArgb(226, 232, 240));
        using var brush = new SolidBrush(ForeColor);
        using var font = new Font("Segoe UI", 7f);
        if (Vertical) e.Graphics.DrawLine(border, thickness - 1, 0, thickness - 1, Height);
        else e.Graphics.DrawLine(border, 0, thickness - 1, Width, thickness - 1);
        for (int i = first; i <= last; i++)
        {
            float pixel = i * step, position = origin + pixel * zoom;
            bool labeled = i % 5 == 0;
            float tick = labeled ? thickness * .35f : thickness * .18f;
            if (Vertical) e.Graphics.DrawLine(pen, thickness - tick, position, thickness - 1, position);
            else e.Graphics.DrawLine(pen, position, thickness - tick, position, thickness - 1);
            if (!labeled) continue;
            string label = pixel.ToString("0.##");
            if (Vertical)
            {
                var state = e.Graphics.Save();
                e.Graphics.TranslateTransform(2, position + 3); e.Graphics.RotateTransform(-90);
                e.Graphics.DrawString(label, font, brush, 0, 0); e.Graphics.Restore(state);
            }
            else e.Graphics.DrawString(label, font, brush, position + 3, 1);
        }
    }
}
