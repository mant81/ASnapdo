namespace ASnapdo.Models;

public enum AnnotationKind { Rectangle, Arrow, Text, Number, Highlight, Image, PixelPatch }

public sealed record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public AnnotationKind Kind { get; init; }
    public RectangleF Bounds { get; set; }
    public Color Color { get; set; } = Color.FromArgb(37, 99, 235);
    public float Width { get; set; } = 3;
    public string Text { get; set; } = "";

    public bool Filled { get; set; }
    public bool Dashed { get; set; }
    public float FontSize { get; set; } = 24;
    public bool Bold { get; set; }
    public bool ReverseX { get; set; }
    public bool ReverseY { get; set; }
    // Encoded pixels are immutable and shared safely by undo snapshots.
    public string ImageData { get; init; } = "";
    public bool IsSelectable => Kind != AnnotationKind.PixelPatch;

    public Annotation Copy(bool newIdentity = false) => this with { Id = newIdentity ? Guid.NewGuid() : Id };

    public void Draw(Graphics g)
    {
        var b = Bounds;
        using var pen = new Pen(Color, Width) { DashStyle = Dashed ? System.Drawing.Drawing2D.DashStyle.Dash : System.Drawing.Drawing2D.DashStyle.Solid };
        using var brush = new SolidBrush(Color);
        switch (Kind)
        {
            case AnnotationKind.Image:
                using (var image = Services.ImageSafety.LoadBase64(ImageData)) g.DrawImage(image, b);
                break;
            case AnnotationKind.PixelPatch:
                var state = g.Save();
                try
                {
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    using var image = Services.ImageSafety.LoadBase64(ImageData); g.DrawImage(image, b);
                }
                finally { g.Restore(state); }
                break;
            case AnnotationKind.Rectangle:
                if (Filled) { using var fill = new SolidBrush(System.Drawing.Color.FromArgb(55, Color)); g.FillRectangle(fill, b); }
                g.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height); break;
            case AnnotationKind.Arrow:
                using (var cap = new System.Drawing.Drawing2D.AdjustableArrowCap(4, 5))
                {
                    pen.CustomEndCap = cap;
                    g.DrawLine(pen, ReverseX ? b.Right : b.Left, ReverseY ? b.Bottom : b.Top, ReverseX ? b.Left : b.Right, ReverseY ? b.Top : b.Bottom);
                }
                break;
            case AnnotationKind.Highlight:
                using (var translucent = new SolidBrush(System.Drawing.Color.FromArgb(70, Color))) g.FillRectangle(translucent, b);
                break;
            case AnnotationKind.Number:
                g.FillEllipse(brush, b);
                using (var font = new Font("Segoe UI", Math.Max(1, Math.Min(b.Height * .52f, b.Width * .95f / Math.Max(1, Text.Length))), FontStyle.Bold, GraphicsUnit.Pixel))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(Text, font, Brushes.White, b, format);
                break;
            case AnnotationKind.Text:
                using (var font = new Font("Segoe UI", FontSize, Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                using (var format = new StringFormat(StringFormat.GenericTypographic))
                    g.DrawString(Text, font, brush, b, format);
                break;
        }
    }
}
