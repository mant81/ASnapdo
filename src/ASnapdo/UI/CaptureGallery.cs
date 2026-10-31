using ASnapdo.Models;
using System.Drawing.Drawing2D;

namespace ASnapdo.UI;

public sealed class CaptureGallery : UserControl
{
    private readonly FlowLayoutPanel strip = new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Padding = new Padding(8, 4, 8, 0) };
    private readonly ToolTip tips = new();
    public event Action<CaptureDocument>? DocumentSelected;
    public event EventHandler? CloseRequested;
    public int ThumbnailCount => strip.Controls.Count;
    public CaptureGallery()
    {
        BackColor = Color.FromArgb(248, 250, 252); AccessibleName = "캡처 갤러리";
        var closeArea = new Panel { Dock = DockStyle.Right, Width = 40, Padding = new Padding(4) };
        var close = new ChromeButton { IconKey = "close", Dock = DockStyle.Top, Height = 32, AccessibleName = "캡처 갤러리 닫기" };
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        closeArea.Controls.Add(close);
        Controls.Add(strip); Controls.Add(closeArea); Height = 112;
    }
    public void SetDocuments(IReadOnlyList<CaptureDocument> documents, CaptureDocument? selected)
    {
        var old = strip.Controls.OfType<Thumbnail>().ToArray();
        if (old.Length != documents.Count || old.Where((item, i) => i < documents.Count && !ReferenceEquals(item.Document, documents[i])).Any())
        {
            Clear();
            foreach (var document in documents)
            {
                var item = new Thumbnail(document) { Width = 132, Height = 88, Margin = new Padding(4, 0, 4, 0) };
                item.Click += (_, _) => DocumentSelected?.Invoke(document);
                tips.SetToolTip(item, document.Name + $" · {document.Image.Width} × {document.Image.Height}"); strip.Controls.Add(item);
            }
        }
        foreach (var item in strip.Controls.OfType<Thumbnail>()) item.Update(ReferenceEquals(item.Document, selected));
    }
    public void Clear()
    {
        tips.RemoveAll();
        foreach (var control in strip.Controls.Cast<Control>().ToArray()) control.Dispose();
        strip.Controls.Clear();
    }
    protected override void Dispose(bool disposing) { if (disposing) { Clear(); tips.Dispose(); } base.Dispose(disposing); }

    private sealed class Thumbnail(CaptureDocument document) : Control
    {
        public CaptureDocument Document { get; } = document;
        private Bitmap? thumbnail;
        private int revision = -1;
        private bool selected;
        public void Update(bool active)
        {
            DoubleBuffered = true;
            TabStop = true; AccessibleRole = AccessibleRole.PushButton; AccessibleName = Document.Name; Cursor = Cursors.Hand;
            if (revision != Document.Revision || thumbnail == null)
            {
                var next = new Bitmap(120, 58);
                try
                {
                    using var g = Graphics.FromImage(next); g.Clear(Color.White);
                    float scale = Math.Min(120f / Document.Image.Width, 58f / Document.Image.Height);
                    g.TranslateTransform((120 - Document.Image.Width * scale) / 2, (58 - Document.Image.Height * scale) / 2);
                    g.ScaleTransform(scale, scale); g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(Document.Image, 0, 0, Document.Image.Width, Document.Image.Height);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    foreach (var annotation in Document.Annotations) annotation.Draw(g);
                }
                catch { next.Dispose(); throw; }
                thumbnail?.Dispose(); thumbnail = next; revision = Document.Revision;
            }
            selected = active; Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.Clear(selected ? Color.FromArgb(231, 240, 255) : Color.White);
            if (thumbnail != null) e.Graphics.DrawImage(thumbnail, new Rectangle(6, 4, Width - 12, Height - 27));
            string caption = Document.Name;
            if (caption.Length >= 23 && DateTime.TryParseExact(caption[..19], "yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var captured))
                caption = captured.ToString("HH:mm:ss") + caption[19..^4];
            TextRenderer.DrawText(e.Graphics, caption, Font, new Rectangle(5, Height - 21, Width - 10, 18), Color.FromArgb(71, 85, 105), TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            using var pen = new Pen(selected || Focused ? Color.FromArgb(37, 99, 235) : Color.FromArgb(226, 232, 240), selected ? 2 : 1);
            e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) { e.SuppressKeyPress = true; OnClick(EventArgs.Empty); } else base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) thumbnail?.Dispose(); base.Dispose(disposing); }
    }
}
