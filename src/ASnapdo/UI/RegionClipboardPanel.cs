namespace ASnapdo.UI;

public sealed class RegionClipboardPanel : UserControl
{
    private readonly ListBox preview = new() { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false, BorderStyle = BorderStyle.None, AccessibleName = "복사한 영역 목록" };
    private readonly Label details = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.SlateGray };
    private readonly ChromeButton paste = new() { Text = "현재 탭에 붙여넣기", IconKey = "paste", Primary = true, Dock = DockStyle.Fill };
    private bool canPaste;
    public Image? Snapshot => preview.SelectedItem as Image;
    public int ItemCount => preview.Items.Count;
    public event EventHandler? ContentsChanged;
    public event EventHandler? PasteRequested;
    public event EventHandler? Dismissed;

    public RegionClipboardPanel()
    {
        BackColor = Color.White; Padding = new Padding(12); Visible = false;
        BorderStyle = BorderStyle.FixedSingle;
        AccessibleName = "최근 복사 영역";
        preview.ItemHeight = (int)(72 * DeviceDpi / 96f);
        preview.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var image = (Image)preview.Items[e.Index];
            using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(231, 240, 255) : Color.White);
            e.Graphics.FillRectangle(background, e.Bounds);
            int pad = Math.Max(4, (int)(6 * DeviceDpi / 96f));
            int box = e.Bounds.Height - pad * 2;
            float scale = Math.Min((float)box / image.Width, (float)box / image.Height);
            int w = Math.Max(1, (int)(image.Width * scale)), h = Math.Max(1, (int)(image.Height * scale));
            e.Graphics.DrawImage(image, new Rectangle(e.Bounds.X + pad + (box - w) / 2, e.Bounds.Y + pad + (box - h) / 2, w, h));
            TextRenderer.DrawText(e.Graphics, $"{image.Width} × {image.Height} px", Font,
                new Rectangle(e.Bounds.X + box + pad * 2, e.Bounds.Y, Math.Max(0, e.Bounds.Width - box - pad * 3), e.Bounds.Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        preview.SelectedIndexChanged += (_, _) => UpdateActions();
        preview.DoubleClick += (_, _) => { if (paste.Enabled) PasteRequested?.Invoke(this, EventArgs.Empty); };
        preview.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; } };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var header = new Panel { Dock = DockStyle.Fill };
        var close = new ChromeButton { IconKey = "close", Width = 32, Dock = DockStyle.Right, AccessibleName = "클립보드 패널 닫기" };
        close.Click += (_, _) => { Visible = false; Dismissed?.Invoke(this, EventArgs.Empty); };
        header.Controls.Add(new Label { Text = "클립보드", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 11, FontStyle.Bold) });
        header.Controls.Add(close);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(details, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        var delete = new ChromeButton { Text = "선택 삭제", IconKey = "delete", Width = 116, Height = 28 };
        var clear = new ChromeButton { Text = "모두 비우기", Width = 116, Height = 28 };
        delete.Click += (_, _) => DeleteSelected(); clear.Click += (_, _) => Clear();
        actions.Controls.AddRange([delete, clear]);
        layout.Controls.Add(preview, 0, 2); layout.Controls.Add(actions, 0, 3); layout.Controls.Add(paste, 0, 4);
        paste.Click += (_, _) => PasteRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(layout);
    }
    public void ShowSnapshot(Image image)
    {
        var replacement = new Bitmap(image);
        preview.BeginUpdate();
        try
        {
            preview.Items.Insert(0, replacement);
            long pixels = preview.Items.Cast<Image>().Sum(item => (long)item.Width * item.Height);
            while (ItemCount > 1 && (ItemCount > 12 || pixels > 32_000_000))
            {
                var oldest = (Image)preview.Items[ItemCount - 1]; pixels -= (long)oldest.Width * oldest.Height;
                preview.Items.RemoveAt(ItemCount - 1); oldest.Dispose();
            }
            preview.SelectedIndex = 0; preview.TopIndex = 0;
        }
        finally { preview.EndUpdate(); }
        Visible = true; UpdateActions(); ContentsChanged?.Invoke(this, EventArgs.Empty);
    }
    public int GetCardHeight(int cardWidth, int availableHeight)
    {
        var layout = (TableLayoutPanel)Controls[0];
        int fixedHeight = Padding.Vertical + 2 + (int)layout.RowStyles.Cast<RowStyle>()
            .Where(row => row.SizeType == SizeType.Absolute).Sum(row => row.Height);
        return Math.Max(0, Math.Min(availableHeight, fixedHeight + Math.Clamp(ItemCount, 1, 3) * preview.ItemHeight + 8));
    }
    public void SelectItem(int index) => preview.SelectedIndex = index;
    private void UpdateActions() { details.Text = $"복사한 영역 {ItemCount}개 · 최대 12개"; paste.Enabled = canPaste && Snapshot != null; }
    public void SetPasteEnabled(bool enabled) { canPaste = enabled; UpdateActions(); }
    public void DeleteSelected()
    {
        int index = preview.SelectedIndex;
        if (index < 0) return;
        var image = (Image)preview.Items[index]; preview.Items.RemoveAt(index); image.Dispose();
        if (ItemCount > 0) preview.SelectedIndex = Math.Min(index, ItemCount - 1);
        UpdateActions(); ContentsChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Clear()
    {
        var images = preview.Items.Cast<Image>().ToArray(); preview.Items.Clear();
        foreach (var image in images) image.Dispose();
        UpdateActions(); ContentsChanged?.Invoke(this, EventArgs.Empty);
    }
    protected override void Dispose(bool disposing) { if (disposing) { ContentsChanged = null; Clear(); } base.Dispose(disposing); }
}
