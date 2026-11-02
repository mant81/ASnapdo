using ASnapdo.Models;
using ASnapdo.Services;
using ASnapdo.UI;

namespace ASnapdo;

public sealed class MainForm : Form
{
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(260, 36), Padding = new Point(18, 7) };
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ClipboardService clipboard = new();
    private readonly CaptureGallery gallery = new() { Visible = false };
    private readonly RegionClipboardPanel regionClipboard = new();
    private readonly NotifyIcon tray;
    private readonly GlobalHotkeyService hotkeys;
    private readonly AppSettings settings;
    private Label? selectedLabel;
    private Panel? homePanel;
    private Panel? ribbonPanel;
    private Action? relayout;
    private Action? syncProperties;
    private readonly List<(Button Button, AnnotationKind? Kind)> ribbonToolButtons = [];
    private readonly List<Control> documentActions = [];
    private readonly List<ToolStripItem> documentStatusItems = [];
    private readonly List<ColorSwatch> swatches = [];
    private readonly ToolTip tips = new() { InitialDelay = 500, ReshowDelay = 100, AutoPopDelay = 5000 };
    private NumericUpDown? strokeControl;
    private bool exiting, capturing, updatingInspector;
    private bool deferCaptureStartup, startupInitialized;
    private readonly bool captureOnlyStartup;
    private Action? initializeStartup;
    private EditorCanvas? Editor => tabs.SelectedTab?.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<EditorCanvas>().FirstOrDefault();

    public MainForm(bool managedTray = false, string? startupAction = null)
    {
        captureOnlyStartup = managedTray && startupAction is "--capture-region" or "--capture-full";
        deferCaptureStartup = captureOnlyStartup;
        Icon = AppIcon.Load();
        Disposed += (_, _) => Icon?.Dispose();
        var version = typeof(MainForm).Assembly.GetName().Version;
        Text = version != null ? $"ASnapdo v{version.ToString(3)}" : "ASnapdo";
        Width = 1160; Height = 620; MinimumSize = new Size(900, 500); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(245, 247, 250);
        settings = SettingsService.Load();
        var header = new TableLayoutPanel { Dock = DockStyle.None, Height = 56, BackColor = Color.White, Padding = new Padding(20, 8, 10, 8), ColumnCount = 2, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        var headerActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 3, 0, 0), Margin = Padding.Empty, BackColor = Color.White };
        header.Controls.Add(headerActions, 0, 0);
        var newButton = HeaderButton("새 캡처", true); newButton.Width = 132; var captureMenu = new ContextMenuStrip();
        captureMenu.Items.Add("영역 캡처", null, (_, _) => _ = CaptureAsync(true));
        captureMenu.Items.Add("전체 화면", null, (_, _) => _ = CaptureAsync(false));
        newButton.Text = "새 캡처";
        newButton.Margin = new Padding(0, 0, 0, 0);
        newButton.Click += (_, _) => _ = CaptureAsync(settings.NewCaptureSelectRegion);
        newButton.ContextMenuStrip = captureMenu;
        Disposed += (_, _) => captureMenu.Dispose(); headerActions.Controls.Add(newButton); tips.SetToolTip(newButton, "옵션에서 지정한 방식으로 캡처 · 오른쪽 클릭: 캡처 방식 선택");
        AddHeaderCommand(headerActions, "열기", OpenImage); documentActions.Add(AddHeaderCommand(headerActions, "저장", () => Save()));
        AddHeaderSeparator(headerActions);
        documentActions.Add(AddHeaderCommand(headerActions, "실행 취소", Undo, 110));
        var settingsButton = HeaderButton("설정", false); settingsButton.Margin = Padding.Empty;
        ((ChromeButton)settingsButton).IconKey = "settings";
        settingsButton.AccessibleName = "설정"; settingsButton.Click += (_, _) => Configure(); tips.SetToolTip(settingsButton, "단축키 · 자동 저장 · 저장 폴더 설정");
        var helpButton = HeaderButton("도움말", false); helpButton.Margin = Padding.Empty;
        ((ChromeButton)helpButton).IconKey = "help";
        helpButton.AccessibleName = "도움말"; helpButton.Click += (_, _) => { using var dialog = new HelpForm(); dialog.ShowDialog(this); };
        tips.SetToolTip(helpButton, "라이선스 정책 · 주요 기능 · 업데이트 안내");
        var utilityLinks = new Panel { BackColor = Color.White, Padding = Padding.Empty, Margin = Padding.Empty, Dock = DockStyle.Fill };
        settingsButton.Width = helpButton.Width = 62;
        utilityLinks.Controls.Add(settingsButton);
        var utilitySeparator = new Label { Text = "|", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Silver };
        utilityLinks.Controls.Add(utilitySeparator);
        utilityLinks.Controls.Add(helpButton);
        header.Controls.Add(utilityLinks, 1, 0);


        void SetInk(Color chosen)
        {
            if (Editor is not { } editor) return;
            editor.Ink = chosen;
            if (editor.Document.Annotations.Find(a => a.Id == editor.Document.Selection) is { } selected)
            { editor.Document.Checkpoint(); selected.Color = chosen; editor.NotifyEdit(); }

            UpdateSwatches(chosen);
        }
        var width = new NumericUpDown { Minimum = 1, Maximum = 30, Value = 3, Width = 70, Height = 30, Margin = new Padding(4, 6, 4, 0), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10) };
        width.AccessibleName = "선 두께"; tips.SetToolTip(width, "선 두께 (px)");
        width.ValueChanged += (_, _) =>
        {
            if (updatingInspector) return;
            if (Editor is not { } editor) return;
            editor.Stroke = (float)width.Value;
            if (editor.Document.Annotations.Find(a => a.Id == editor.Document.Selection) is { } a)
            { editor.Document.Checkpoint(); a.Width = editor.Stroke; editor.NotifyEdit(); }
        };

        strokeControl = width;

        ribbonPanel = new Panel { Dock = DockStyle.None, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(20, 5, 8, 2), Visible = false };
        var ribbon = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 0, 4, 0), BackColor = Color.FromArgb(248, 250, 252) };
        ribbonPanel.Controls.Add(ribbon);
        FlowLayoutPanel AddRibbonGroup(string title, int groupWidth, bool inRibbon = true)
        {
            var group = new Panel { Width = groupWidth, Height = 42, Margin = new Padding(0, 0, 6, 0), BackColor = Color.Transparent, AccessibleName = title };
            if (inRibbon && ribbon.Controls.Count > 0)
            {
                var separator = new Panel { Width = 1, Height = 24, BackColor = Color.FromArgb(226, 232, 240), Margin = new Padding(5, 6, 8, 0) };
                ribbon.Controls.Add(separator);
                group.VisibleChanged += (_, _) => separator.Visible = group.Visible;
            }
            var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(2, 1, 2, 0), BackColor = Color.Transparent };
            var caption = new Label { Text = title, Dock = DockStyle.Bottom, Height = 18, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8) };
            group.Controls.Add(content); caption.Dispose(); if (inRibbon) ribbon.Controls.Add(group); return content;
        }
        Button RibbonButton(string title, string icon, Action action, AnnotationKind? kind = null)
        {
            var button = new ChromeButton { Text = title, IconKey = icon switch { "↖" => "select", "↗" => "arrow", "□" when title == "맞춤" => "fit", "□" => "rectangle", "T" => "text", "①" => "number", "▰" => "highlight", "−" => "minus", "+" => "plus", _ => "copy" }, IconAboveText = false, Width = 102, Height = 36, BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 8.5f), Margin = new Padding(1, 0, 1, 0), AccessibleName = title, Tag = kind };
            button.Click += (_, _) => { Editor?.FinishTextEdit(); Editor?.CancelCrop(); action(); if (Editor != null) Editor.Document.Selection = null; RefreshStatus(); };
            tips.SetToolTip(button, title);
            if (kind.HasValue || title == "선택") ribbonToolButtons.Add((button, kind));
            return button;
        }
        var selectGroup = AddRibbonGroup("선택", 110);
        selectGroup.Padding = new Padding(0, 1, 2, 0);
        var selectButton = RibbonButton("선택", "↖", () => { if (Editor != null) Editor.Tool = null; });
        selectButton.Margin = new Padding(0, 0, 1, 0);
        selectGroup.Controls.Add(selectButton);
        var editGroup = AddRibbonGroup("주석", 530);
        editGroup.Controls.Add(RibbonButton("화살표", "↗", () => { if (Editor != null) Editor.Tool = AnnotationKind.Arrow; }, AnnotationKind.Arrow));
        editGroup.Controls.Add(RibbonButton("사각형", "□", () => { if (Editor != null) Editor.Tool = AnnotationKind.Rectangle; }, AnnotationKind.Rectangle));
        editGroup.Controls.Add(RibbonButton("텍스트", "T", () => { if (Editor != null) Editor.Tool = AnnotationKind.Text; }, AnnotationKind.Text));
        editGroup.Controls.Add(RibbonButton("번호", "①", () => { if (Editor != null) Editor.Tool = AnnotationKind.Number; }, AnnotationKind.Number));
        editGroup.Controls.Add(RibbonButton("형광펜", "▰", () => { if (Editor != null) Editor.Tool = AnnotationKind.Highlight; }, AnnotationKind.Highlight));
        var cropTool = new ChromeButton { Text = "자르기", IconKey = "crop", Width = 102, Height = 36, BackColor = Color.FromArgb(248, 250, 252), Margin = new Padding(1, 0, 1, 0), AccessibleName = "선택 자르기" };
        var effectsGroup = AddRibbonGroup("이미지 편집", 340);
        cropTool.Click += (_, _) => Editor?.StartCrop(); effectsGroup.Controls.Add(cropTool);
        tips.SetToolTip(cropTool, "선택 자르기 · 드래그 후 Enter로 적용, Esc로 취소");
        var mosaicTool = new ChromeButton { Text = "모자이크", IconKey = "mosaic", Width = 110, Height = 36, AccessibleName = "모자이크" };
        var blurTool = new ChromeButton { Text = "블러", IconKey = "blur", Width = 104, Height = 36, AccessibleName = "블러" };
        mosaicTool.Click += (_, _) => Editor?.StartPixelEffect(PixelEffectKind.Mosaic); effectsGroup.Controls.Add(mosaicTool);
        blurTool.Click += (_, _) => Editor?.StartPixelEffect(PixelEffectKind.Blur); effectsGroup.Controls.Add(blurTool);
        tips.SetToolTip(mosaicTool, "캡처 이미지 영역을 드래그해 모자이크 · 강도 조절 후 Enter 적용");
        tips.SetToolTip(blurTool, "캡처 이미지 영역을 드래그해 블러 · 강도 조절 후 Enter 적용");
        var regionGroup = AddRibbonGroup("이미지 영역", 350);
        var regionCopy = new ChromeButton { Text = "영역 복사", IconKey = "region-copy", Width = 110, Height = 36, AccessibleName = "선택한 이미지 영역 복사" };
        regionCopy.Click += (_, _) => Editor?.StartCrop(true); regionGroup.Controls.Add(regionCopy);
        var regionPaste = new ChromeButton { Text = "붙여넣기", IconKey = "paste", Width = 110, Height = 36 };
        regionPaste.Click += (_, _) => PasteImage(); regionGroup.Controls.Add(regionPaste);
        var clipboardToggle = new ChromeButton { Text = "클립보드", IconKey = "paste", Width = 110, Height = 36, AccessibleName = "복사한 영역 목록 열기" };
        clipboardToggle.Click += (_, _) => { regionClipboard.Visible = !regionClipboard.Visible; relayout?.Invoke(); };
        regionGroup.Controls.Add(clipboardToggle);
        tips.SetToolTip(regionCopy, "드래그를 놓으면 영역 자동 복사 · Esc: 취소");
        tips.SetToolTip(regionPaste, "클립보드 이미지를 이동 가능한 객체로 붙여넣기 (Ctrl+V)");
        var colorGroup = AddRibbonGroup("색상", 240, false);
        foreach (var swatch in new[] { Color.FromArgb(239, 68, 68), Color.FromArgb(37, 99, 235), Color.FromArgb(22, 163, 74), Color.FromArgb(245, 158, 11), Color.FromArgb(217, 70, 137), Color.FromArgb(51, 65, 85) })
        {
            var chip = new ColorSwatch { Width = 28, Height = 28, SwatchColor = swatch, BackColor = Color.FromArgb(248, 250, 252), Margin = new Padding(2, 3, 2, 1), AccessibleName = $"색상 #{swatch.R:X2}{swatch.G:X2}{swatch.B:X2}" };
            chip.Click += (_, _) => SetInk(swatch); colorGroup.Controls.Add(chip); swatches.Add(chip);
        }
        var customColor = new ChromeButton { Text = "", IconKey = "plus", Width = 30, Height = 28, BackColor = Color.White, ForeColor = Color.FromArgb(37, 99, 235), Margin = new Padding(4, 3, 2, 1), AccessibleName = "사용자 지정 색상" };
        customColor.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = Editor?.Ink ?? Color.FromArgb(37, 99, 235) };
            if (picker.ShowDialog(this) == DialogResult.OK) SetInk(picker.Color);
        };
        colorGroup.Controls.Add(customColor);
        var thicknessGroup = AddRibbonGroup("두께", 160, false);
        thicknessGroup.Controls.Add(new Label { Text = "두께", AutoSize = true, Margin = new Padding(2, 8, 2, 0) });
        thicknessGroup.Controls.Add(width);
        thicknessGroup.Controls.Add(new Label { Text = "px", AutoSize = true, Margin = new Padding(2, 8, 2, 0) });

        var properties = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, WrapContents = false, AutoScroll = true, Padding = new Padding(8, 6, 0, 0), BackColor = Color.White };
        ribbonPanel.Controls.Add(properties);
        properties.BringToFront(); ribbon.BringToFront();
        var propertiesLabel = new Label { Text = "속성", AutoSize = true, Margin = new Padding(2, 7, 12, 0), ForeColor = Color.SlateGray };
        properties.Controls.Add(propertiesLabel);
        selectedLabel = new Label { Text = "선택", Width = 70, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
        properties.Controls.Add(selectedLabel);
        properties.Controls.Add(colorGroup.Parent!);
        properties.Controls.Add(thicknessGroup.Parent!);
        var fill = new CheckBox { Text = "채우기", AutoSize = true, Margin = new Padding(8, 7, 8, 0) };
        var dashed = new CheckBox { Text = "점선", AutoSize = true, Margin = new Padding(8, 7, 8, 0) };
        var fontSize = new NumericUpDown { Minimum = 8, Maximum = 144, Value = 24, Width = 62, Margin = new Padding(4, 4, 6, 0) };
        var bold = new CheckBox { Text = "굵게", AutoSize = true, Margin = new Padding(6, 7, 12, 0) };
        var fontSizeLabel = new Label { Text = "글자 크기", AutoSize = true, Margin = new Padding(12, 7, 2, 0) };
        properties.Controls.AddRange([fill, dashed, fontSizeLabel, fontSize, bold]);
        var cropApply = new ChromeButton { Text = "자르기 적용", IconKey = "check", Primary = true, Width = 138, Height = 32, Visible = false };
        cropApply.Click += (_, _) => { if (Editor?.RegionCopyMode == true) CopyRegion(); else if (Editor?.PixelEffect != null) ApplyPixelEffect(); else ApplyCrop(); };
        var cropCancel = new ChromeButton { Text = "취소", IconKey = "close", Width = 84, Height = 32, BackColor = Color.White, Visible = false };
        cropCancel.Click += (_, _) => Editor?.CancelCrop();
        var cropSize = new Label { AutoSize = true, Visible = false, Margin = new Padding(8, 7, 0, 0) };
        var effectLabel = new Label { Text = "강도 (px)", AutoSize = true, Visible = false, Margin = new Padding(6, 7, 0, 0) };
        var effectStrength = new NumericUpDown { Minimum = 2, Maximum = 64, Value = 12, Width = 62, Visible = false, Margin = new Padding(4, 4, 8, 0), AccessibleName = "효과 강도" };
        effectStrength.ValueChanged += (_, _) => { if (!updatingInspector) Editor?.SetEffectStrength((int)effectStrength.Value); };
        effectStrength.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyPixelEffect(); Editor?.Focus(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Editor?.CancelCrop(); Editor?.Focus(); }
        };
        void ApplyStyle() { if (!updatingInspector) Editor?.SetStyleOptions(fill.Checked, dashed.Checked, (float)fontSize.Value, bold.Checked); }
        fill.CheckedChanged += (_, _) => ApplyStyle(); dashed.CheckedChanged += (_, _) => ApplyStyle();
        fontSize.ValueChanged += (_, _) => ApplyStyle(); bold.CheckedChanged += (_, _) => ApplyStyle();
        var duplicate = new ChromeButton { Text = "복제", IconKey = "copy", Width = 80, Height = 32, BackColor = Color.White };
        var delete = new ChromeButton { Text = "삭제", IconKey = "delete", Width = 84, Height = 32, BackColor = Color.White };
        var help = new Label { AutoSize = true, ForeColor = Color.SlateGray, Margin = new Padding(14, 7, 0, 0) };
        syncProperties = () =>
        {
            properties.SuspendLayout();
            ribbon.SuspendLayout();
            try
            {
            cropTool.Selected = Editor?.CropMode == true && !Editor.RegionCopyMode && Editor.PixelEffect == null;
            mosaicTool.Selected = Editor?.PixelEffect == PixelEffectKind.Mosaic;
            blurTool.Selected = Editor?.PixelEffect == PixelEffectKind.Blur;
            regionCopy.Selected = Editor?.RegionCopyMode == true;
            cropApply.Text = Editor?.RegionCopyMode == true ? "영역 복사" : Editor?.PixelEffect is { } effect ? effect == PixelEffectKind.Mosaic ? "모자이크 적용" : "블러 적용" : "자르기 적용";
            bool cropping = Editor?.CropMode == true;
            propertiesLabel.Visible = selectedLabel.Visible = !cropping;
            cropApply.Visible = cropCancel.Visible = cropSize.Visible = cropping;
            if (Editor?.RegionCopyMode == true) cropApply.Visible = false;
            effectLabel.Visible = effectStrength.Visible = Editor?.PixelEffect != null;
            if (Editor?.PixelEffect != null)
            {
                updatingInspector = true; try { effectStrength.Value = Editor.EffectStrength; } finally { updatingInspector = false; }
            }
            cropApply.Enabled = Editor?.CanApplyCrop == true;
            cropSize.Text = Editor?.RegionCopyMode == true
                ? (Editor.CanApplyCrop ? $"{Editor.CropBounds.Width} × {Editor.CropBounds.Height} px · 놓으면 자동 복사 / Esc 취소" : "복사할 영역을 드래그하세요 · 놓으면 자동 복사 / Esc 취소")
                : Editor?.CanApplyCrop == true ? $"{Editor.CropBounds.Width} × {Editor.CropBounds.Height} px · Enter 적용 / Esc 취소" : "영역을 드래그하세요 · Esc 취소";
            if (Editor is not { } ed) return;
            var a = ed.Selected;
            var kind = a?.Kind ?? ed.Tool;
            fill.Visible = !cropping && kind == AnnotationKind.Rectangle;
            dashed.Visible = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Arrow;
            fontSizeLabel.Visible = fontSize.Visible = bold.Visible = !cropping && kind == AnnotationKind.Text;
            duplicate.Visible = delete.Visible = !cropping && a != null;
            bool hasStroke = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Arrow;
            foreach (Control control in thicknessGroup.Controls) control.Visible = hasStroke;
            thicknessGroup.Parent!.Visible = hasStroke;
            bool hasColor = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Arrow or AnnotationKind.Text or AnnotationKind.Number or AnnotationKind.Highlight;
            colorGroup.Parent!.Visible = hasColor;
            help.Visible = !cropping;
            help.Text = a?.Kind is AnnotationKind.Text or AnnotationKind.Number ? "더블클릭: 수정 · Delete: 삭제"
                : a != null ? "드래그: 이동 · 핸들: 크기 조절"
                : kind == AnnotationKind.Number ? "클릭: 연속 번호 추가 · Esc: 선택"
                : kind == AnnotationKind.Text ? "클릭: 텍스트 입력 · Ctrl+Enter: 확정"
                : kind != null ? "드래그로 주석 추가 · Esc: 선택" : "객체를 클릭해 선택하거나 편집 도구를 고르세요";
            updatingInspector = true;
            try { fill.Checked = a?.Filled ?? ed.Filled; dashed.Checked = a?.Dashed ?? ed.Dashed; fontSize.Value = Math.Clamp((decimal)(a?.FontSize ?? ed.TextSize), 8, 144); bold.Checked = a?.Bold ?? ed.Bold; }
            finally { updatingInspector = false; }
            }
            finally
            {
                properties.ResumeLayout(true);
                ribbon.ResumeLayout(true);
            }
        };
        duplicate.Click += (_, _) => Editor?.DuplicateSelection(); properties.Controls.Add(duplicate);
        tips.SetToolTip(duplicate, "선택 객체 복제 (Ctrl+D)");
        delete.Click += (_, _) => Editor?.DeleteSelection(); properties.Controls.Add(delete);
        properties.Controls.Add(help);
        properties.Controls.AddRange([cropApply, cropCancel, effectLabel, effectStrength, cropSize]);

        var hint = new Label { Text = "도구를 고르고 캔버스에서 드래그해 주석을 추가하세요.", Dock = DockStyle.None, Height = 28, ForeColor = Color.FromArgb(100, 116, 139), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) };
        var workspace = new Panel { BackColor = Color.FromArgb(241, 245, 249) };
        var centerHost = new Panel { Dock = DockStyle.None, BackColor = Color.FromArgb(235, 241, 248) };
        var welcome = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.FromArgb(241, 245, 249), Padding = new Padding(24) };
        welcome.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); welcome.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        welcome.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); welcome.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); welcome.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        welcome.Controls.Add(new Label { Text = "캡처하고, 바로 편집하세요", Font = new Font("Segoe UI Semibold", 19, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        welcome.Controls.Add(new Label { Text = "화면을 캡처하거나 이미지를 열어 시작하세요.", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(100, 116, 139), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 2);
        var welcomeButtonHost = new Panel { Dock = DockStyle.Fill };
        var welcomeButtons = new FlowLayoutPanel { Width = 420, Height = 46, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var welcomeRegion = HeaderButton("영역 캡처", true); welcomeRegion.Width = 132; welcomeRegion.Click += (_, _) => _ = CaptureAsync(true);
        var welcomeFull = HeaderButton("전체 화면", false); welcomeFull.Width = 132; welcomeFull.Click += (_, _) => _ = CaptureAsync(false);
        var welcomeOpen = HeaderButton("이미지 열기", false); welcomeOpen.Width = 132; welcomeOpen.Click += (_, _) => OpenImage();
        welcomeButtons.Controls.AddRange([welcomeRegion, welcomeFull, welcomeOpen]); welcomeButtonHost.Controls.Add(welcomeButtons);
        welcomeButtonHost.Resize += (_, _) => welcomeButtons.Location = new Point(Math.Max(0, (welcomeButtonHost.ClientSize.Width - welcomeButtons.Width) / 2), 8);
        welcome.Controls.Add(welcomeButtonHost, 0, 3);
        homePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(235, 241, 248) };
        homePanel.Controls.Add(welcome); centerHost.Controls.Add(tabs); centerHost.Controls.Add(homePanel);
        tabs.Visible = false;
        workspace.Controls.Add(centerHost);
        workspace.Controls.Add(regionClipboard);
        regionClipboard.Dismissed += (_, _) => relayout?.Invoke();
        regionClipboard.ContentsChanged += (_, _) => relayout?.Invoke();
        regionClipboard.PasteRequested += (_, _) => Guard(() =>
        {
            if (Editor is not { } editor || regionClipboard.Snapshot is not { } image) return;
            editor.FinishTextEdit();
            if (clipboard.PasteImage(editor.Document, image))
            { editor.CancelCrop(); editor.Tool = null; editor.NotifyEdit(); RefreshStatus(); }
        });
        var bar = new StatusStrip { Dock = DockStyle.None, AutoSize = false, BackColor = Color.White, SizingGrip = false, Padding = new Padding(12, 2, 12, 2) }; bar.Items.Add(status);
        var zoomDown = new ToolStripButton("−"); zoomDown.Click += (_, _) => Zoom(.8f); bar.Items.Add(zoomDown); documentStatusItems.Add(zoomDown);
        var zoomUp = new ToolStripButton("+"); zoomUp.Click += (_, _) => Zoom(1.25f); bar.Items.Add(zoomUp); documentStatusItems.Add(zoomUp);
        documentStatusItems.Add(ActionButton(bar, "맞춤", Fit));
        var shell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        shell.Controls.Add(workspace); shell.Controls.Add(bar); shell.Controls.Add(hint); shell.Controls.Add(ribbonPanel); shell.Controls.Add(header); shell.Controls.Add(gallery);
        gallery.DocumentSelected += document =>
        {
            Editor?.FinishTextEdit();
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page => ReferenceEquals(page.Tag, document));
        };
        gallery.CloseRequested += (_, _) =>
        {
            settings.ShowGallery = false;
            relayout?.Invoke(); RefreshStatus();
            try { SettingsService.Save(settings); }
            catch (Exception e) { status.Text = "갤러리를 닫았습니다. 표시 설정 저장 실패: " + e.Message; }
        };
        var toolTitles = ribbonToolButtons.ToDictionary(item => item.Button, item => item.Button.Text);
        var headerTitles = headerActions.Controls.OfType<ChromeButton>().ToDictionary(button => button, button => (button.Text, button.Width));
        void LayoutShell()
        {
            int w = shell.ClientSize.Width, h = shell.ClientSize.Height;
            bool compact = w < 1050;
            foreach (var (button, _) in ribbonToolButtons)
            {
                button.Text = compact ? "" : toolTitles[button];
                button.Width = compact ? 44 : TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 44;
            }
            cropTool.Text = compact ? "" : "자르기"; cropTool.Width = compact ? 44 : TextRenderer.MeasureText(cropTool.Text, cropTool.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 44;
            mosaicTool.Text = compact ? "" : "모자이크"; blurTool.Text = compact ? "" : "블러";
            foreach (var button in new[] { mosaicTool, blurTool })
                button.Width = compact ? 44 : TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 44;
            // Keep the full region action labels visible, including with DPI-scaled fonts.
            foreach (var button in new[] { regionCopy, regionPaste, clipboardToggle })
                button.Width = Math.Max(110, TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 44);
            foreach (var content in new[] { selectGroup, editGroup, effectsGroup, regionGroup, colorGroup, thicknessGroup })
                content.Parent!.Width = content.Padding.Horizontal +
                    content.Controls.Cast<Control>().Sum(control => control.Width + control.Margin.Horizontal) + 4;
            foreach (var (button, originalHeader) in headerTitles)
            {
                bool iconOnly = compact && !button.Primary;
                button.Text = iconOnly ? "" : originalHeader.Text;
                button.Width = iconOnly ? 44 : originalHeader.Width;
            }
            int ribbonHeight = tabs.TabCount > 0 ? 114 : 0;
            int galleryHeight = settings.ShowGallery && tabs.TabCount > 0 ? 112 : 0;
            gallery.Visible = galleryHeight > 0;

            ribbonPanel.Visible = ribbonHeight > 0;

            hint.Visible = false;
            header.Bounds = new Rectangle(0, 0, w, 56);
            settingsButton.Bounds = new Rectangle(0, 0, 88, 36);
            utilitySeparator.Bounds = new Rectangle(88, 0, 14, 36);
            helpButton.Bounds = new Rectangle(102, 0, 104, 36);
            utilityLinks.BringToFront();
            ribbonPanel.Bounds = new Rectangle(0, 56, w, ribbonHeight);
            workspace.Bounds = new Rectangle(0, 56 + ribbonHeight, w, Math.Max(0, h - 88 - ribbonHeight - galleryHeight));
            gallery.Bounds = new Rectangle(0, Math.Max(0, h - 32 - galleryHeight), w, galleryHeight);
            bar.Bounds = new Rectangle(0, Math.Max(0, h - 32), w, 32);
            centerHost.Bounds = new Rectangle(0, 0, workspace.Width, workspace.Height);
            if (regionClipboard.Visible)
            {
                int gap = Math.Max(8, (int)(12 * DeviceDpi / 96f));
                int cardWidth = Math.Min((int)(280 * DeviceDpi / 96f), Math.Max(0, workspace.Width - gap * 2));
                int cardHeight = regionClipboard.GetCardHeight(cardWidth, Math.Max(0, workspace.Height - gap * 2));
                regionClipboard.Bounds = new Rectangle(workspace.Width - cardWidth - gap, gap, cardWidth, cardHeight);
                regionClipboard.BringToFront();
            }
        }
        relayout = LayoutShell;
        shell.Resize += (_, _) => LayoutShell();
        Shown += (_, _) => LayoutShell();
        Controls.Add(shell);
        tabs.DrawItem += (_, e) =>
        {
            var page = tabs.TabPages[e.Index]; var b = tabs.GetTabRect(e.Index);
            bool active = e.Index == tabs.SelectedIndex;
            var document = Document(page);
            using var fill = new SolidBrush(active ? Color.White : Color.FromArgb(244, 247, 251));
            e.Graphics.FillRectangle(fill, b);
            using var line = new Pen(Color.FromArgb(226, 232, 240), active ? 2 : 1);
            e.Graphics.DrawLine(line, b.Left + 3, b.Bottom - 1, b.Right - 3, b.Bottom - 1);
            using var tabFont = new Font("Segoe UI", 8.5f);
            TextRenderer.DrawText(e.Graphics, document.Name, tabFont, new Rectangle(b.X + 12, b.Y, b.Width - 58, b.Height), active ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139), TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (document.Dirty) { using var dot = new SolidBrush(Color.FromArgb(37, 99, 235)); e.Graphics.FillEllipse(dot, b.Right - 43, b.Y + b.Height / 2 - 3, 6, 6); }
            TextRenderer.DrawText(e.Graphics, "×", Font, new Rectangle(b.Right - 26, b.Y, 22, b.Height), Color.FromArgb(100, 116, 139), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        };
        tabs.MouseDown += (_, e) => { for (int i = 0; i < tabs.TabCount; i++) { var b = tabs.GetTabRect(i); if (new Rectangle(b.Right - 24, b.Y, 24, b.Height).Contains(e.Location)) { CloseTab(i, confirmSave: false); break; } } };
        tabs.SelectedIndexChanged += (_, _) => { RefreshStatus(); relayout?.Invoke(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("ASnapdo 열기", null, (_, _) => Restore());
        menu.Items.Add("영역 캡처", null, (_, _) => _ = CaptureAsync(true));
        menu.Items.Add("전체 캡처", null, (_, _) => _ = CaptureAsync(false));
        menu.Items.Add("종료", null, (_, _) => { exiting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "ASnapdo", Visible = !managedTray, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Restore();
        hotkeys = new GlobalHotkeyService(Handle);
        initializeStartup = () =>
        {
            if (startupInitialized) return;
            startupInitialized = true;
            bool region = hotkeys.Set(1, settings.RegionHotkey), full = hotkeys.Set(2, settings.FullscreenHotkey);
            status.Text = region && full ? "영역 또는 전체 화면을 캡처하세요." : "일부 전역 단축키를 등록하지 못했습니다. 설정에서 변경하세요.";
            if (managedTray) BeginInvoke(new Action(() =>
            {
                Guard(() =>
                {
                    var session = SessionService.Load();
                    foreach (var document in session.Documents) AddDocumentCore(document);
                    if (tabs.TabCount > 0) tabs.SelectedIndex = Math.Clamp(session.SelectedIndex, 0, tabs.TabCount - 1);
                    BeginInvoke(new Action(() =>
                    {
                        for (int i = 0; i < Math.Min(tabs.TabCount, session.Scrolls.Count); i++)
                            tabs.TabPages[i].Controls.OfType<Panel>().Single().AutoScrollPosition = session.Scrolls[i];
                        if (startupAction == "--capture-region") _ = CaptureAsync(true);
                        else if (startupAction == "--capture-full") _ = CaptureAsync(false);
                        else if (startupAction == "--settings") Configure();
                    }));
                });
            }));
        };
        Shown += (_, _) => initializeStartup();
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && !exiting)
            {
                if (!managedTray) { e.Cancel = true; Hide(); return; }
                try
                {
                    foreach (TabPage page in tabs.TabPages) page.Controls.OfType<Panel>().Single().Controls.OfType<EditorCanvas>().Single().FinishTextEdit();
                    SessionService.Save(tabs.TabPages.Cast<TabPage>().Select(Document), tabs.TabPages.Cast<TabPage>().Select(page =>
                    {
                        var scroll = page.Controls.OfType<Panel>().Single().AutoScrollPosition; return new Point(-scroll.X, -scroll.Y);
                    }).ToList(), tabs.SelectedIndex);
                }
                catch (Exception error) { e.Cancel = true; status.Text = "편집 상태 보관 실패: " + error.Message; }
            }
        };
        FormClosed += (_, _) => { hotkeys.Dispose(); tray.Dispose(); menu.Dispose(); tips.Dispose(); foreach (TabPage page in tabs.TabPages) Document(page).Dispose(); };
        RefreshStatus();
    }
    protected override void SetVisibleCore(bool value)
    {
        if (value && deferCaptureStartup)
        {
            deferCaptureStartup = false;
            base.SetVisibleCore(false);
            BeginInvoke(new Action(() => initializeStartup?.Invoke()));
            return;
        }
        base.SetVisibleCore(value);
    }
    private static Button HeaderButton(string title, bool primary)
    {
        return new ChromeButton { Text = title, IconKey = title switch { "새 캡처" or "영역 캡처" => "capture", "전체 화면" => "monitor", "열기" or "이미지 열기" => "open", "저장" => "save", "복사" => "copy", "실행 취소" => "undo", _ => "" }, Primary = primary, Width = primary ? 128 : 92, Height = 36, BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(245, 247, 250), ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), AccessibleName = title };
    }
    private Button AddHeaderCommand(FlowLayoutPanel strip, string title, Action action, int width = 92)
    {
        var button = HeaderButton(title, false);
        button.Width = width; button.BackColor = Color.White; button.Font = new Font("Segoe UI", 9f);
        if (button is ChromeButton chrome) chrome.IconKey = title switch { "열기" => "open", "저장" => "save", "복사" => "copy", "실행 취소" => "undo", _ => "" };
        button.Click += (_, _) => action(); strip.Controls.Add(button); tips.SetToolTip(button, title switch { "저장" => "저장 (Ctrl+S)", "복사" => "합성 이미지 복사 (Ctrl+C)", "실행 취소" => "실행 취소 (Ctrl+Z)", _ => title }); return button;
    }
    private static void AddHeaderSeparator(FlowLayoutPanel strip)
    {
        strip.Controls.Add(new Label { Width = 1, Height = 34, BackColor = Color.FromArgb(226, 232, 240), Margin = new Padding(8, 2, 8, 0) });
    }
    private void UpdateRibbonSelection()
    {
        foreach (var (button, kind) in ribbonToolButtons)
        {
            bool active = Editor != null && !Editor.CropMode && Editor.Tool == kind;
            if (button is ChromeButton chrome) chrome.Selected = active;
        }
    }
    private static ToolStripButton ActionButton(ToolStrip strip, string title, Action action) { var item = new ToolStripButton(title) { DisplayStyle = ToolStripItemDisplayStyle.Text, ForeColor = Color.FromArgb(51, 65, 85), Padding = new Padding(8, 3, 8, 3) }; item.Click += (_, _) => action(); strip.Items.Add(item); return item; }
    private static CaptureDocument Document(TabPage page) => (CaptureDocument)page.Tag!;
    private void Restore() { Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
    private void Guard(Action action) { try { action(); } catch (Exception e) { if (captureOnlyStartup && !Visible) Restore(); status.Text = $"작업 실패: {e.Message}"; } }
    private async Task CaptureAsync(bool region)
    {
        if (capturing || !Enabled || OwnedForms.Any(form => form.Visible)) return;
        capturing = true; bool wasVisible = Visible;
        try
        {
            Hide(); await Task.Delay(200);
            Bitmap image;
            if (region)
            {
                using var selection = new SelectionForm();
                if (selection.ShowDialog() != DialogResult.OK) { if (wasVisible) Restore(); return; }
                image = selection.CaptureSelection();
            }
            else image = CaptureService.Capture(Screen.FromPoint(Cursor.Position).Bounds);
            AddDocument(image); Restore();
            if (settings.AutoCopy) CopyImage();
            AutoSaveCapture(Editor!.Document);
        }
        catch (Exception e) { Restore(); status.Text = $"캡처 실패: {e.Message}"; }
        finally
        {
            capturing = false;
            // A cancelled cold capture leaves the saved session untouched and releases
            // the editor so the native tray can register its hotkeys again.
            if (captureOnlyStartup && !Visible) { exiting = true; Close(); }
        }
    }
    private void AutoSaveCapture(CaptureDocument document)
    {
        if (!settings.AutoSave) return;
        try { status.Text = "원본 자동 저장: " + AutoSaveService.SaveOriginal(document, settings.AutoSaveFolder); }
        catch (Exception e) { status.Text = "자동 저장 실패 (캡처는 탭에 유지됩니다): " + e.Message; }
    }
    private void AddDocument(Bitmap image)
    {
        string stem = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"), name = stem + ".png"; int suffix = 2;
        while (tabs.TabPages.Cast<TabPage>().Any(p => Document(p).Name == name)) name = $"{stem}_{suffix++}.png";
        var document = new CaptureDocument(image, name);
        AddDocumentCore(document);
    }
    private void AddDocumentCore(CaptureDocument document)
    {
        string name = document.Name;
        var page = new TabPage(name + " ●") { Tag = document };
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(241, 245, 249), Padding = new Padding(16) };
        var editor = new EditorCanvas(document) { Location = new Point(16, 16) };
        editor.Edited += (_, _) => RefreshStatus();
        editor.SelectionChanged += (_, _) => RefreshStatus();
        editor.RegionCopyRequested += (_, _) => { if (ReferenceEquals(Editor, editor)) CopyRegion(); };
        editor.MouseWheel += (_, e) => { if ((ModifierKeys & Keys.Control) != 0) { if (e is HandledMouseEventArgs handled) handled.Handled = true; Zoom(e.Delta > 0 ? 1.25f : .8f, e.Location); } };
        panel.Resize += (_, _) =>
        {
            editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2) + panel.AutoScrollPosition.X, Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2) + panel.AutoScrollPosition.Y);
        };
        panel.Controls.Add(editor); page.Controls.Add(panel);
        page.Controls.Add(new CanvasRuler(editor, true));
        page.Controls.Add(new CanvasRuler(editor, false));
        tabs.TabPages.Add(page);
        tabs.Visible = true; if (homePanel != null) homePanel.Visible = false;
        tabs.SelectedTab = page; relayout?.Invoke(); tabs.PerformLayout(); page.PerformLayout(); panel.PerformLayout();
        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || editor.IsDisposed) return;
            editor.UpdateSize();
            panel.AutoScrollPosition = Point.Empty;
            editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2), Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2));
            if (Editor == editor) { RefreshStatus(); editor.Focus(); }
        }));
    }
    private void OpenImage() => Guard(() =>
    {
        using var dialog = new OpenFileDialog { Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        AddDocument(ImageSafety.LoadFile(dialog.FileName));
    });
    private bool Save()
    {
        if (Editor is not { } editor) return false;
        editor.FinishTextEdit();
        var document = editor.Document; string? path = document.SavePath;
        if (path == null)
        {
            using var dialog = new SaveFileDialog { Filter = "PNG 이미지|*.png", FileName = document.Name, DefaultExt = "png", OverwritePrompt = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try
        {
            using var image = document.Compose(); image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            document.SavePath = path; document.Saved(); RefreshStatus(); tabs.Invalidate(); return true;
        }
        catch (Exception e) { status.Text = $"저장 실패: {e.Message}"; return false; }
    }
    private void CopyRegion() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit();
        if (!editor.CanApplyCrop) { status.Text = "영역 복사를 누르고 복사할 범위를 드래그하세요."; return; }
        if (clipboard.CopyRegion(editor.Document, editor.CropBounds))
        {
            using var image = Clipboard.GetImage();
            if (image != null) regionClipboard.ShowSnapshot(image);
            editor.CancelCrop(); relayout?.Invoke();
            status.Text = "영역을 복사했습니다. 다른 탭에서 Ctrl+V로 붙여넣으세요.";
        }
    });
    private void PasteImage() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit();
        if (!clipboard.PasteImage(editor.Document)) { status.Text = "클립보드에 이미지가 없습니다."; return; }
        editor.CancelCrop(); editor.Tool = null; editor.NotifyEdit(); RefreshStatus();
    });
    private void CopyImage() => Guard(() =>
    {
        if (Editor == null) return;
        Editor.FinishTextEdit();
        clipboard.CopyImage(Editor.Document);
        Editor.ShowCopyFeedback();
        status.Text = "✓ 캡처 이미지가 클립보드에 복사되었습니다. Ctrl+V로 붙여넣으세요.";
    });
    private void Undo()
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit(); editor.CancelCrop(); var oldSize = editor.Document.Image.Size;
        editor.Document.Undo();
        if (editor.Document.Image.Size != oldSize) ResetCanvasViewport(editor);
        editor.Invalidate(); RefreshStatus();
    }
    private void ApplyCrop()
    {
        if (Editor is not { } editor) return;
        Guard(() => { if (editor.ApplyCrop()) { ResetCanvasViewport(editor); RefreshStatus(); } });
    }
    private void ApplyPixelEffect() => Guard(() => { if (Editor?.ApplyPixelEffect() == true) RefreshStatus(); });
    private static void ResetCanvasViewport(EditorCanvas editor)
    {
        editor.UpdateSize();
        if (editor.Parent is not Panel panel) return;
        panel.AutoScrollPosition = Point.Empty;
        editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2), Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2));
    }
    private bool CloseTab(int index, bool confirmSave = true)
    {
        tabs.SelectedIndex = index; var page = tabs.TabPages[index]; var document = Document(page);
        if (confirmSave && document.Dirty)
        {
            var answer = MessageBox.Show(this, $"{document.Name}\n변경 내용을 저장할까요?\n예: 저장 / 아니요: 저장 안 함", "ASnapdo", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save())) return false;
        }
        tabs.TabPages.Remove(page); page.Dispose(); document.Dispose();
        if (tabs.TabCount == 0) { tabs.Visible = false; if (homePanel != null) homePanel.Visible = true; }
        relayout?.Invoke();
        RefreshStatus(); return true;
    }
    private void Zoom(float factor, Point? anchor = null)
    {
        if (Editor is not { } editor || editor.Parent is not Panel panel) return;
        var point = anchor ?? new Point(editor.Width / 2, editor.Height / 2);
        var viewport = panel.PointToClient(editor.PointToScreen(point));
        float old = editor.Document.Zoom; editor.Document.Zoom = Math.Clamp(old * factor, .05f, 8);
        panel.AutoScrollPosition = Point.Empty; editor.UpdateSize();
        editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2), Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2));
        panel.AutoScrollPosition = new Point(Math.Max(0, editor.Left + (int)(point.X * editor.Document.Zoom / old) - viewport.X), Math.Max(0, editor.Top + (int)(point.Y * editor.Document.Zoom / old) - viewport.Y));
        RefreshStatus();
    }
    private void Fit()
    {
        if (Editor is not { } editor || editor.Parent is not Panel panel) return;
        editor.Document.Zoom = Math.Clamp(Math.Min(1f, Math.Min((panel.ClientSize.Width - 40f) / editor.Document.Image.Width, (panel.ClientSize.Height - 40f) / editor.Document.Image.Height)), .05f, 1);
        panel.AutoScrollPosition = Point.Empty; editor.UpdateSize();
        editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2), Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2));
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        regionClipboard.SetPasteEnabled(Editor != null);
        foreach (TabPage page in tabs.TabPages) page.Text = Document(page).Name + (Document(page).Dirty ? " ●" : "");
        status.Text = Editor is { } e ? $"{e.Document.Image.Width} × {e.Document.Image.Height} · PNG · {(e.Document.Dirty ? "변경됨" : "저장됨")}     {e.Document.Zoom:P0}" : "영역 또는 전체 화면을 캡처하세요.";
        foreach (var action in documentActions) action.Enabled = Editor != null;
        foreach (var action in documentStatusItems) action.Enabled = Editor != null;
        var selected = Editor?.Document.Annotations.Find(a => a.Id == Editor.Document.Selection);
        if (selectedLabel != null)
        {
            selectedLabel.Text = (selected?.Kind ?? Editor?.Tool) switch
            {
                AnnotationKind.Arrow => "화살표",
                AnnotationKind.Rectangle => "사각형",
                AnnotationKind.Text => "텍스트",
                AnnotationKind.Number => "번호",
                AnnotationKind.Highlight => "형광펜",
                AnnotationKind.Image => "이미지",
                _ => "선택"
            };
        }
        if (Editor is { } activeEditor)
        {
            var ink = selected?.Color ?? activeEditor.Ink;
            UpdateSwatches(ink);
            if (strokeControl != null)
            {
                updatingInspector = true;
                try { strokeControl.Value = Math.Clamp((decimal)(selected?.Width ?? activeEditor.Stroke), strokeControl.Minimum, strokeControl.Maximum); }
                finally { updatingInspector = false; }
            }
        }
        UpdateRibbonSelection();
        syncProperties?.Invoke();
        if (settings.ShowGallery && tabs.TabCount > 0)
            gallery.SetDocuments(tabs.TabPages.Cast<TabPage>().Select(Document).ToArray(), Editor?.Document);
        else if (gallery.ThumbnailCount > 0) gallery.Clear();
        tabs.Invalidate();
    }
    private void UpdateSwatches(Color color)
    {
        foreach (var swatch in swatches) swatch.Selected = swatch.SwatchColor.ToArgb() == color.ToArgb();
    }
    private void Configure()
    {
        using var dialog = new OptionsForm(settings, ApplyOptions, hotkeys.CheckAvailability, StartupService.Available ? StartupService.Enabled : null, StartupService.SetEnabled);
        dialog.ShowDialog(this);
    }
    private string? ApplyOptions(AppSettings updated)
    {
        var oldRegion = settings.RegionHotkey; var oldFull = settings.FullscreenHotkey;
        bool changed = updated.RegionHotkey != oldRegion || updated.FullscreenHotkey != oldFull;
        if (changed)
        {
            hotkeys.Dispose();
            if (!hotkeys.Set(1, updated.RegionHotkey) || !hotkeys.Set(2, updated.FullscreenHotkey))
            {
                hotkeys.Dispose();
                bool r = hotkeys.Set(1, oldRegion), f = hotkeys.Set(2, oldFull);
                return r && f ? "단축키 등록에 실패해 기존 키로 복원했습니다. 다른 키를 지정하세요." : "단축키 등록과 일부 복원에 실패했습니다. 다른 키를 지정하세요.";
            }
        }
        try { SettingsService.Save(updated); }
        catch (Exception e)
        {
            bool restored = true;
            if (changed) { hotkeys.Dispose(); bool r = hotkeys.Set(1, oldRegion), f = hotkeys.Set(2, oldFull); restored = r && f; }
            return "설정 저장 실패: " + e.Message + (restored ? "" : " · 단축키 복원도 실패했습니다.");
        }
        settings.RegionHotkey = updated.RegionHotkey; settings.FullscreenHotkey = updated.FullscreenHotkey;
        settings.NewCaptureSelectRegion = updated.NewCaptureSelectRegion; settings.AutoCopy = updated.AutoCopy;
        settings.AutoSave = updated.AutoSave; settings.AutoSaveFolder = updated.AutoSaveFolder;
        settings.ShowGallery = updated.ShowGallery;
        relayout?.Invoke(); RefreshStatus();
        return null;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x8000 + 41)
        {
            if (!Enabled || OwnedForms.Any(form => form.Visible)) return;
            int action = m.WParam.ToInt32();
            if (action == 1 || action == 2) _ = CaptureAsync(action == 1);
            else { Restore(); if (action == 3) Configure(); }
            return;
        }
        if (m.Msg == 0x8000 + 42)
        {
            foreach (var dialog in OwnedForms) dialog.Close();
            Guard(SessionService.Clear); exiting = true; Close(); return;
        }
        if (m.Msg == 0x0312) { _ = CaptureAsync(m.WParam.ToInt32() == 1); return; }
        base.WndProc(ref m);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Control? focused = ActiveControl;
        while (focused is ContainerControl container && container.ActiveControl != null) focused = container.ActiveControl;
        if (Editor?.IsEditingText == true) return base.ProcessCmdKey(ref msg, keyData);
        if (focused is TextBoxBase || focused is NumericUpDown || focused is ComboBox) return base.ProcessCmdKey(ref msg, keyData);
        switch (keyData)
        {
            case Keys.Control | Keys.C: CopyImage(); return true;
            case Keys.Control | Keys.V: PasteImage(); return true;
            case Keys.Control | Keys.S: Save(); return true;
            case Keys.Control | Keys.Z: Undo(); return true;
            case Keys.Control | Keys.W: if (tabs.SelectedIndex >= 0) CloseTab(tabs.SelectedIndex); return true;
            case Keys.Control | Keys.Tab: if (tabs.TabCount > 0) tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.TabCount; return true;
            case Keys.Control | Keys.D0: Fit(); return true;
            case Keys.Control | Keys.D: Editor?.DuplicateSelection(); return true;
            case Keys.Enter when Editor?.RegionCopyMode == true: CopyRegion(); return true;
            case Keys.Enter when Editor?.PixelEffect != null: ApplyPixelEffect(); return true;
            case Keys.Enter when Editor?.CropMode == true: ApplyCrop(); return true;
            case Keys.Escape: if (Editor != null) { Editor.CancelCrop(); Editor.Tool = null; Editor.Document.Selection = null; Editor.Invalidate(); RefreshStatus(); } return true;
            case Keys.Delete: Editor?.DeleteSelection(); return true;
            case Keys.Control | Keys.Shift | Keys.C: if (Editor != null) clipboard.CopyAnnotations(Editor.Document, false); return true;
            case Keys.Control | Keys.Alt | Keys.C:
                if (Editor?.CropMode == true) { CopyRegion(); return true; }
                if (Editor != null) { clipboard.CopyAnnotations(Editor.Document, true); status.Text = Editor.Document.Selection == null ? "먼저 주석을 선택하세요." : "선택 주석을 복사했습니다."; } return true;
            case Keys.Control | Keys.Shift | Keys.V: if (Editor != null) { clipboard.Paste(Editor.Document); Editor.Invalidate(); RefreshStatus(); } return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
