using ASnapdo.Models;

namespace ASnapdo.Services;

public sealed class ClipboardService
{
    private List<Annotation> buffer = [];
    private Size sourceSize;
    public bool CopyRegion(CaptureDocument document, Rectangle region)
    {
        region.Intersect(new Rectangle(Point.Empty, document.Image.Size));
        if (region.Width < 1 || region.Height < 1) return false;
        using var composed = document.Compose();
        using var image = composed.Clone(region, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Clipboard.SetDataObject(image, true, 5, 100);
        return true;
    }
    public bool PasteImage(CaptureDocument document)
    {
        using var image = Clipboard.GetImage();
        if (image == null) return false;
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        float scale = Math.Min(1f, Math.Min((float)document.Image.Width / image.Width, (float)document.Image.Height / image.Height));
        var width = Math.Max(1, image.Width * scale);
        var height = Math.Max(1, image.Height * scale);
        var annotation = new Annotation { Kind = AnnotationKind.Image, ImageData = Convert.ToBase64String(stream.ToArray()),
            Bounds = new((document.Image.Width - width) / 2, (document.Image.Height - height) / 2, width, height) };
        document.Checkpoint(); document.Annotations.Add(annotation); document.Selection = annotation.Id; document.Changed();
        return true;
    }
    public void CopyImage(CaptureDocument document)
    {
        using var image = document.Compose();
        Clipboard.SetDataObject(image, true, 5, 100);
    }
    public void CopyAnnotations(CaptureDocument document, bool selectedOnly)
    {
        buffer = document.Annotations.Where(a => a.IsSelectable && (!selectedOnly || a.Id == document.Selection)).Select(a => a.Copy()).ToList();
        sourceSize = document.Image.Size;
    }
    public void Paste(CaptureDocument document)
    {
        if (buffer.Count == 0) return;
        document.Checkpoint();
        float sx = (float)document.Image.Width / sourceSize.Width, sy = (float)document.Image.Height / sourceSize.Height;
        foreach (var item in buffer)
        {
            var a = item.Copy(true);
            var b = a.Bounds;
            var width = Math.Clamp(b.Width * sx, 1, document.Image.Width);
            var height = Math.Clamp(b.Height * sy, 1, document.Image.Height);
            a.Bounds = new(Math.Clamp(b.X * sx, 0, document.Image.Width - width), Math.Clamp(b.Y * sy, 0, document.Image.Height - height), width, height);
            a.FontSize = Math.Clamp(a.FontSize * Math.Min(sx, sy), 8, 144);
            a.Width = Math.Max(1, a.Width * Math.Min(sx, sy));
            document.Annotations.Add(a);
        }
        document.Changed();
    }
}
