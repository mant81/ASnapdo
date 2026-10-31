using ASnapdo.Models;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ASnapdo.UI;

public sealed partial class EditorCanvas : Control
{
    public CaptureDocument Document { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationKind? Tool { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Ink { get; set; } = Color.FromArgb(37, 99, 235);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Stroke { get; set; } = 3;
    public bool Filled { get; private set; }
    public bool Dashed { get; private set; }
    public float TextSize { get; private set; } = 24;
    public bool Bold { get; private set; }
    public bool IsEditingText => textEditor?.ContainsFocus == true || numberEditor?.ContainsFocus == true;
    public Annotation? Selected => Document.Annotations.Find(a => a.Id == Document.Selection);
    public event EventHandler? Edited;
    public event EventHandler? SelectionChanged;
    private PointF start;
    private Annotation? draft;
    private RectangleF original;
    private int handle = -1;
    private bool checkpointed;
    private TextBox? textEditor;
    private Annotation? editingAnnotation;
    private bool editingNew;
    private NumericUpDown? numberEditor;
    private Annotation? editingNumber;

    public EditorCanvas(CaptureDocument document)
    {
        Document = document; DoubleBuffered = true; TabStop = true; BackColor = Color.White;
        UpdateSize();
    }
    public void UpdateSize()
    {
        FinishTextEdit();
        Size = new Size(Math.Max(1, (int)(Document.Image.Width * Document.Zoom)), Math.Max(1, (int)(Document.Image.Height * Document.Zoom)));
        Invalidate();
    }
    private PointF ImagePoint(Point p) => new(Math.Clamp(p.X / Document.Zoom, 0, Document.Image.Width), Math.Clamp(p.Y / Document.Zoom, 0, Document.Image.Height));
    private static PointF[] Handles(RectangleF b) => [new(b.Left, b.Top), new(b.Left + b.Width / 2, b.Top), new(b.Right, b.Top), new(b.Right, b.Top + b.Height / 2), new(b.Right, b.Bottom), new(b.Left + b.Width / 2, b.Bottom), new(b.Left, b.Bottom), new(b.Left, b.Top + b.Height / 2)];
    private int HitHandle(PointF p)
    {
        if (Selected is not { } a) return -1;
        var handles = Handles(a.Bounds);
        for (int i = 0; i < handles.Length; i++)
            if (Math.Abs(handles[i].X - p.X) <= 6 / Document.Zoom && Math.Abs(handles[i].Y - p.Y) <= 6 / Document.Zoom) return i;
        return -1;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.ScaleTransform(Document.Zoom, Document.Zoom);
        e.Graphics.DrawImage(Document.Image, 0, 0, Document.Image.Width, Document.Image.Height);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var a in Document.Annotations) if (a != editingAnnotation) a.Draw(e.Graphics);
        if (effectPreview != null)
        {
            var previewState = e.Graphics.Save(); e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(effectPreview, CropBounds); e.Graphics.Restore(previewState);
        }
        draft?.Draw(e.Graphics);
        if (CropMode) { PaintCrop(e.Graphics); return; }
        if (Selected is not { } selected || textEditor != null) return;
        var b = selected.Bounds;
        using var pen = new Pen(Color.FromArgb(37, 99, 235), 1 / Document.Zoom);
        pen.DashStyle = DashStyle.Dash;
        e.Graphics.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height);
        pen.DashStyle = DashStyle.Solid;
        float h = 7 / Document.Zoom;
        foreach (var p in Handles(b))
        {
            e.Graphics.FillRectangle(Brushes.White, p.X - h / 2, p.Y - h / 2, h, h);
            e.Graphics.DrawRectangle(pen, p.X - h / 2, p.Y - h / 2, h, h);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        FinishTextEdit(); Focus(); start = ImagePoint(e.Location); checkpointed = false;
        if (CropMode) { BeginCropDrag(start); return; }
        if (Tool == AnnotationKind.Number && Document.Annotations.LastOrDefault(a => a.IsSelectable && a.Bounds.Contains(start)) is { Kind: AnnotationKind.Number } existing)
        {
            Document.Selection = existing.Id; Tool = null;
        }
        if (Tool is { } kind)
        {
            Document.Selection = null;
            draft = new Annotation { Kind = kind, Color = Ink, Width = Stroke, Filled = Filled, Dashed = Dashed, FontSize = TextSize, Bold = Bold, Bounds = new(start.X, start.Y, 1, 1) };
            if (kind == AnnotationKind.Text)
            {
                draft.Bounds = new(start.X, start.Y, Math.Min(320, Document.Image.Width - start.X), Math.Min(100, Document.Image.Height - start.Y));
                var a = draft; draft = null; BeginTextEdit(a, true); return;
            }
            if (kind == AnnotationKind.Number)
            {
                draft.Text = Math.Min(9999L, Document.Annotations.Where(a => a.Kind == AnnotationKind.Number).Select(a => int.TryParse(a.Text, out var n) ? (long)n : 0).DefaultIfEmpty().Max() + 1).ToString();
                float size = Math.Min(36, Math.Min(Document.Image.Width, Document.Image.Height));
                draft.Bounds = new(Math.Min(start.X, Document.Image.Width - size), Math.Min(start.Y, Document.Image.Height - size), size, size);
                CommitDraft(); return;
            }
        }
        else
        {
            handle = HitHandle(start);
            if (handle < 0) Document.Selection = Document.Annotations.LastOrDefault(a => a.IsSelectable && a.Bounds.Contains(start))?.Id;
            if (Selected is { } a) original = a.Bounds;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        Capture = true; Invalidate();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || Tool != null) return;
        Capture = false;
        if (Selected is { Kind: AnnotationKind.Text } a) BeginTextEdit(a, false);
        else if (Selected is { Kind: AnnotationKind.Number } number) BeginNumberEdit(number);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = ImagePoint(e.Location);
        if (CropMode) { MoveCropPointer(p); return; }
        if (!Capture)
        {
            int hit = HitHandle(p);
            Cursor = Tool != null ? (Tool == AnnotationKind.Text ? Cursors.IBeam : Cursors.Cross) : hit is 0 or 4 ? Cursors.SizeNWSE : hit is 2 or 6 ? Cursors.SizeNESW : hit is 1 or 5 ? Cursors.SizeNS : hit is 3 or 7 ? Cursors.SizeWE : Document.Annotations.Any(a => a.IsSelectable && a.Bounds.Contains(p)) ? Cursors.SizeAll : Cursors.Default;
            return;
        }
        if (draft != null)
        {
            if ((ModifierKeys & Keys.Shift) != 0 && draft.Kind == AnnotationKind.Rectangle)
            {
                float side = Math.Min(Math.Abs(p.X - start.X), Math.Abs(p.Y - start.Y));
                p = new(start.X + Math.Sign(p.X - start.X) * side, start.Y + Math.Sign(p.Y - start.Y) * side);
            }
            draft.ReverseX = p.X < start.X; draft.ReverseY = p.Y < start.Y;
            draft.Bounds = RectangleF.FromLTRB(Math.Min(start.X, p.X), Math.Min(start.Y, p.Y), Math.Max(start.X, p.X), Math.Max(start.Y, p.Y));
        }
        else if (Selected is { } a)
        {
            RectangleF next;
            if (handle >= 0)
            {
                float l = original.Left, r = original.Right, t = original.Top, b = original.Bottom;
                if (handle is 0 or 6 or 7) l = Math.Min(p.X, r - 1);
                if (handle is 2 or 3 or 4) r = Math.Max(p.X, l + 1);
                if (handle is 0 or 1 or 2) t = Math.Min(p.Y, b - 1);
                if (handle is 4 or 5 or 6) b = Math.Max(p.Y, t + 1);
                next = RectangleF.FromLTRB(l, t, r, b);
            }
            else next = new(Math.Clamp(original.X + p.X - start.X, 0, Math.Max(0, Document.Image.Width - original.Width)), Math.Clamp(original.Y + p.Y - start.Y, 0, Math.Max(0, Document.Image.Height - original.Height)), original.Width, original.Height);
            if (next != a.Bounds)
            {
                if (!checkpointed) { Document.Checkpoint(); checkpointed = true; }
                a.Bounds = next;
            }
        }
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!Capture || e.Button != MouseButtons.Left) return;
        if (CropMode) { MoveCropPointer(ImagePoint(e.Location)); Capture = false; RefreshEffectPreview(); return; }
        Capture = false;
        if (draft != null) CommitDraft();
        else if (checkpointed) NotifyEdit();
    }
    private void CommitDraft()
    {
        if (draft is { } a && (a.Bounds.Width >= 2 || a.Bounds.Height >= 2))
        {
            a.Bounds = new(a.Bounds.X, a.Bounds.Y, Math.Max(1, a.Bounds.Width), Math.Max(1, a.Bounds.Height));
            Document.Checkpoint(); Document.Annotations.Add(a); Document.Selection = a.Id;
            if (a.Kind != AnnotationKind.Number) Tool = null;
            NotifyEdit(); SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        draft = null; Invalidate();
    }
    private void BeginTextEdit(Annotation a, bool isNew)
    {
        editingAnnotation = a; editingNew = isNew; Document.Selection = isNew ? null : a.Id;
        textEditor = new TextBox { Multiline = true, AcceptsReturn = true, Text = a.Text, BorderStyle = BorderStyle.FixedSingle, ForeColor = a.Color,
            Font = new Font("Segoe UI", Math.Max(6, a.FontSize * Document.Zoom), a.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel),
            Bounds = Rectangle.Round(new RectangleF(a.Bounds.X * Document.Zoom, a.Bounds.Y * Document.Zoom, Math.Max(30, a.Bounds.Width * Document.Zoom), Math.Max(30, a.Bounds.Height * Document.Zoom))) };
        var input = textEditor;
        input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; FinishTextEdit(true); Focus(); }
            else if (e.Control && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FinishTextEdit(); Focus(); }
        };
        input.LostFocus += (_, _) => FinishTextEdit();
        Controls.Add(input); input.Focus(); input.SelectAll(); Invalidate();
    }
    public void FinishTextEdit(bool cancel = false)
    {
        FinishNumberEdit(cancel);
        if (textEditor is not { } input || editingAnnotation is not { } a) return;
        textEditor = null; editingAnnotation = null;
        string text = input.Text;
        Controls.Remove(input); var font = input.Font; input.Dispose(); font.Dispose();
        if (!cancel && !string.IsNullOrWhiteSpace(text) && (editingNew || text != a.Text))
        {
            Document.Checkpoint(); a.Text = text;
            using var measureFont = new Font("Segoe UI", a.FontSize, a.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            using var g = CreateGraphics();
            var measured = g.MeasureString(text, measureFont, Math.Max(1, (int)a.Bounds.Width), StringFormat.GenericTypographic);
            a.Bounds = new(a.Bounds.X, a.Bounds.Y, a.Bounds.Width, Math.Min(Document.Image.Height - a.Bounds.Y, Math.Max(a.Bounds.Height, measured.Height + 8)));
            if (editingNew) Document.Annotations.Add(a);
            Document.Selection = a.Id; Tool = null; NotifyEdit();
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
    }
    private void BeginNumberEdit(Annotation a)
    {
        editingNumber = a;
        numberEditor = new NumericUpDown
        {
            Minimum = 1, Maximum = 9999,
            Value = int.TryParse(a.Text, out var number) ? Math.Clamp(number, 1, 9999) : 1,
            Location = new Point(Math.Max(0, Math.Min((int)(a.Bounds.X * Document.Zoom), Width - 90)), (int)(a.Bounds.Y * Document.Zoom)),
            Width = Math.Min(90, Width), AccessibleName = "번호 수정"
        };
        var input = numberEditor;
        input.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Escape)
            { e.SuppressKeyPress = true; FinishNumberEdit(e.KeyCode == Keys.Escape); Focus(); }
        };
        input.Leave += (_, _) => FinishNumberEdit();
        Controls.Add(input); input.Focus(); input.Select(0, input.Text.Length);
    }
    private void FinishNumberEdit(bool cancel = false)
    {
        if (numberEditor is not { } input || editingNumber is not { } a) return;
        numberEditor = null; editingNumber = null;
        var value = ((int)input.Value).ToString();
        Controls.Remove(input); input.Dispose();
        if (!cancel && value != a.Text)
        { Document.Checkpoint(); a.Text = value; NotifyEdit(); }
    }
    public void SetStyleOptions(bool filled, bool dashed, float textSize, bool bold)
    {
        Filled = filled; Dashed = dashed; TextSize = textSize; Bold = bold;
        if (Selected is { } a && (a.Filled != filled || a.Dashed != dashed || a.FontSize != textSize || a.Bold != bold))
        {
            Document.Checkpoint(); a.Filled = filled; a.Dashed = dashed; a.FontSize = textSize; a.Bold = bold; NotifyEdit();
        }
    }
    public void DuplicateSelection()
    {
        FinishTextEdit();
        if (Selected is not { } a) return;
        Document.Checkpoint(); var copy = a.Copy(true); var b = copy.Bounds;
        copy.Bounds = new(Math.Min(b.X + 16, Math.Max(0, Document.Image.Width - b.Width)), Math.Min(b.Y + 16, Math.Max(0, Document.Image.Height - b.Height)), b.Width, b.Height);
        Document.Annotations.Add(copy); Document.Selection = copy.Id; NotifyEdit();
    }
    public void NotifyEdit() { Document.Changed(); Invalidate(); Edited?.Invoke(this, EventArgs.Empty); }
    public void DeleteSelection()
    {
        if (Selected == null) return;
        Document.Checkpoint(); Document.Annotations.RemoveAll(a => a.Id == Document.Selection); Document.Selection = null; SelectionChanged?.Invoke(this, EventArgs.Empty); NotifyEdit();
    }
}
