using System.Drawing.Drawing2D;

namespace ASnapdo.UI;

using ASnapdo.Services;

public sealed partial class EditorCanvas
{
    public bool CropMode { get; private set; }
    public Rectangle CropBounds { get; private set; }
    public bool CanApplyCrop => CropMode && CropBounds.Width > 0 && CropBounds.Height > 0;
    private Rectangle cropOriginal;
    private int cropHandle = -2; // -2: new selection, -1: move, 0..7: resize.

    public bool RegionCopyMode { get; private set; }
    public PixelEffectKind? PixelEffect { get; private set; }
    public int EffectStrength { get; private set; } = 12;
    private Bitmap? effectPreview;
    public void StartCrop(bool copyRegion = false)
    {
        FinishTextEdit(); Capture = false; draft = null; Tool = null; Document.Selection = null;
        RegionCopyMode = copyRegion;
        PixelEffect = null; ClearEffectPreview();
        CropMode = true; CropBounds = Rectangle.Empty; Cursor = Cursors.Cross;
        SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate(); Focus();
    }
    public void StartPixelEffect(PixelEffectKind effect)
    {
        StartCrop(); PixelEffect = effect;
        EffectStrength = effect == PixelEffectKind.Mosaic ? 12 : 8;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void SetEffectStrength(int strength)
    {
        EffectStrength = Math.Clamp(strength, 2, 64); RefreshEffectPreview();
    }
    private void ClearEffectPreview() { effectPreview?.Dispose(); effectPreview = null; }
    private void RefreshEffectPreview()
    {
        ClearEffectPreview();
        if (PixelEffect is { } effect && CanApplyCrop && !Capture)
            effectPreview = Document.PreviewPixelEffect(CropBounds, effect, EffectStrength);
        Invalidate();
    }
    public bool ApplyPixelEffect()
    {
        if (!CanApplyCrop || PixelEffect is not { } effect) return false;
        bool changed = Document.ApplyPixelEffect(CropBounds, effect, EffectStrength);
        CancelCrop(); if (changed) NotifyEdit(); return changed;
    }
    protected override void Dispose(bool disposing) { if (disposing) ClearEffectPreview(); base.Dispose(disposing); }
    public void CancelCrop()
    {
        if (!CropMode) return;
        Capture = false; CropMode = false; RegionCopyMode = false; PixelEffect = null; ClearEffectPreview(); CropBounds = Rectangle.Empty; Cursor = Cursors.Default;
        SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
    }
    public bool ApplyCrop()
    {
        if (!CanApplyCrop || RegionCopyMode || PixelEffect != null) return false;
        bool changed = Document.Crop(CropBounds);
        CancelCrop();
        if (changed) { UpdateSize(); NotifyEdit(); }
        return changed;
    }
    private int CropHitHandle(PointF point)
    {
        if (!CanApplyCrop) return -1;
        var handles = Handles(CropBounds);
        for (int i = 0; i < handles.Length; i++)
            if (Math.Abs(point.X - handles[i].X) <= 6 / Document.Zoom && Math.Abs(point.Y - handles[i].Y) <= 6 / Document.Zoom) return i;
        return -1;
    }
    private void BeginCropDrag(PointF point)
    {
        ClearEffectPreview();
        cropOriginal = CropBounds;
        cropHandle = CropHitHandle(point);
        if (cropHandle < 0 && !CropBounds.Contains(Point.Round(point))) { cropHandle = -2; CropBounds = Rectangle.Empty; }
        Capture = true; SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
    }
    private void MoveCropPointer(PointF point)
    {
        if (!Capture)
        {
            int hit = CropHitHandle(point);
            Cursor = hit is 0 or 4 ? Cursors.SizeNWSE : hit is 2 or 6 ? Cursors.SizeNESW : hit is 1 or 5 ? Cursors.SizeNS : hit is 3 or 7 ? Cursors.SizeWE : CropBounds.Contains(Point.Round(point)) ? Cursors.SizeAll : Cursors.Cross;
            return;
        }
        int x = (int)Math.Round(point.X), y = (int)Math.Round(point.Y);
        int sx = (int)Math.Round(start.X), sy = (int)Math.Round(start.Y);
        if (cropHandle == -2)
            CropBounds = Rectangle.FromLTRB(Math.Min(sx, x), Math.Min(sy, y), Math.Max(sx, x), Math.Max(sy, y));
        else if (cropHandle == -1)
            CropBounds = new(Math.Clamp(cropOriginal.X + x - sx, 0, Document.Image.Width - cropOriginal.Width), Math.Clamp(cropOriginal.Y + y - sy, 0, Document.Image.Height - cropOriginal.Height), cropOriginal.Width, cropOriginal.Height);
        else
        {
            int l = cropOriginal.Left, r = cropOriginal.Right, t = cropOriginal.Top, b = cropOriginal.Bottom;
            if (cropHandle is 0 or 6 or 7) l = Math.Min(x, r - 1);
            if (cropHandle is 2 or 3 or 4) r = Math.Max(x, l + 1);
            if (cropHandle is 0 or 1 or 2) t = Math.Min(y, b - 1);
            if (cropHandle is 4 or 5 or 6) b = Math.Max(y, t + 1);
            CropBounds = Rectangle.FromLTRB(l, t, r, b);
        }
        CropBounds = Rectangle.Intersect(new Rectangle(Point.Empty, Document.Image.Size), CropBounds);
        SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
    }
    private void PaintCrop(Graphics g)
    {
        var b = CropBounds;
        using var shaded = new Region(new Rectangle(Point.Empty, Document.Image.Size));
        if (CanApplyCrop) shaded.Exclude(b);
        using var shade = new SolidBrush(Color.FromArgb(100, 15, 23, 42)); g.FillRegion(shade, shaded);
        if (!CanApplyCrop) return;
        using var outline = new Pen(Color.FromArgb(37, 99, 235), 1 / Document.Zoom);
        g.DrawRectangle(outline, b);
        using var thirds = new Pen(Color.FromArgb(170, Color.White), 1 / Document.Zoom) { DashStyle = DashStyle.Dash };
        for (int i = 1; PixelEffect == null && i < 3; i++)
        {
            g.DrawLine(thirds, b.Left + b.Width * i / 3f, b.Top, b.Left + b.Width * i / 3f, b.Bottom);
            g.DrawLine(thirds, b.Left, b.Top + b.Height * i / 3f, b.Right, b.Top + b.Height * i / 3f);
        }
        float h = 7 / Document.Zoom;
        foreach (var p in Handles(b))
        {
            g.FillRectangle(Brushes.White, p.X - h / 2, p.Y - h / 2, h, h);
            g.DrawRectangle(outline, p.X - h / 2, p.Y - h / 2, h, h);
        }
    }
}
