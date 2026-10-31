using ASnapdo.Services;

namespace ASnapdo.UI;

public sealed class OptionsForm : Form
{
    public OptionsForm(AppSettings settings, Func<AppSettings, string?> apply, Func<Keys, HotkeyAvailability> checkHotkey, bool? runAtLogin = null, Action<bool>? setRunAtLogin = null)
    {
        Icon = AppIcon.Load(); Disposed += (_, _) => Icon?.Dispose();
        Text = "ASnapdo 설정"; ClientSize = new Size(580, 560);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; BackColor = Color.White;
        var heading = new Panel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(24, 12, 24, 8), BackColor = Color.White };
        heading.Controls.Add(new Label { Text = "설정 중에는 캡처 단축키가 실행되지 않습니다.", Dock = DockStyle.Bottom, Height = 24, ForeColor = Color.FromArgb(100, 116, 139) });
        heading.Controls.Add(new Label { Text = "내게 맞는 캡처 환경", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) });
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 8) };
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0) };
        body.Controls.Add(tabs);
        Controls.Add(body); Controls.Add(heading);
        FlowLayoutPanel Page(string title)
        {
            var page = new TabPage(title) { BackColor = Color.White };
            var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18, 12, 18, 12), AutoScroll = true, BackColor = Color.White };
            page.Controls.Add(content); tabs.TabPages.Add(page);
            content.Resize += (_, _) =>
            {
                int available = Math.Max(100, content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
                foreach (Control control in content.Controls)
                {
                    if (control is Label label) label.MaximumSize = new Size(available, 0);
                    else if (control is TextBox or ComboBox or HotkeySelector or TableLayoutPanel) control.Width = available;
                }
            };
            return content;
        }
        var layout = Page("캡처");
        var capturePage = layout;
        void Label(string text) => layout.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.FromArgb(71, 85, 105), Margin = new Padding(0, 10, 0, 8) });
        Label("새 캡처 버튼의 기본 동작");
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 400, AccessibleName = "새 캡처 기본 동작" };
        mode.Items.AddRange(["영역 선택 — 드래그로 범위 지정", "전체 화면 — 마우스가 있는 모니터"]);
        mode.SelectedIndex = settings.NewCaptureSelectRegion ? 0 : 1; layout.Controls.Add(mode);
        layout = Page("단축키");
        var shortcutsPage = layout;
        Label("캡처 단축키를 선택하세요.");
        (HotkeySelector Input, Label Status) Hotkey(string label, Keys keys)
        {
            Label(label);
            var input = new HotkeySelector(keys) { AccessibleName = label };
            var status = new Label { AutoSize = true, MaximumSize = new Size(440, 0), Margin = new Padding(3, 1, 0, 3) };
            layout.Controls.Add(input); layout.Controls.Add(status); return (input, status);
        }
        var region = Hotkey("영역 선택 캡처", settings.RegionHotkey);
        var full = Hotkey("전체 화면 캡처", settings.FullscreenHotkey);
        var autoCopy = new CheckBox { Text = "캡처 직후 이미지 복사", Checked = settings.AutoCopy, AutoSize = true, Margin = new Padding(0, 16, 0, 8) };
        capturePage.Controls.Add(autoCopy);
        capturePage.Controls.Add(new Label { Text = "캡처한 원본을 클립보드에 바로 복사합니다.", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(0, 0, 0, 10) });
        layout = Page("저장");
        var autoSave = new CheckBox { Text = "캡처 원본 자동 저장 (PNG)", Checked = settings.AutoSave, AutoSize = true, AccessibleName = "자동 저장" };
        layout.Controls.Add(autoSave);
        Label("자동 저장 폴더");
        var folder = new TextBox { Text = settings.AutoSaveFolder, Width = 330, Dock = DockStyle.Fill, AccessibleName = "자동 저장 폴더" };
        var browse = new ChromeButton { Text = "찾아보기", IconKey = "open", Dock = DockStyle.Fill, Height = 34 };
        var folderRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Width = 460, Height = 40, Margin = Padding.Empty };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        folderRow.Controls.Add(folder, 0, 0); folderRow.Controls.Add(browse, 1, 0);
        layout.Controls.Add(folderRow);
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "캡처 원본을 자동 저장할 폴더", UseDescriptionForTitle = true };
            if (Directory.Exists(folder.Text)) dialog.SelectedPath = folder.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath;
        };
        void UpdateFolderState() { folder.Enabled = browse.Enabled = autoSave.Checked; }
        autoSave.CheckedChanged += (_, _) => UpdateFolderState(); UpdateFolderState();
        Label("캡처 직후 원본을 저장합니다. 편집 결과는 Ctrl+S로 저장하세요.");
        layout = Page("일반");
        var startup = new CheckBox { Text = "Windows 로그인 시 자동 실행", AutoSize = true, Checked = runAtLogin == true, Enabled = runAtLogin.HasValue, AccessibleName = "자동 실행" };
        layout.Controls.Add(startup);
        Label(runAtLogin.HasValue ? "로그인하면 트레이에서 조용히 대기합니다. 캡처할 때만 편집기를 실행합니다." : "설치형 버전에서 자동 실행을 설정할 수 있습니다.");
        var showGallery = new CheckBox { Text = "하단 캡처 갤러리 표시", Checked = settings.ShowGallery, AutoSize = true, AccessibleName = "캡처 갤러리" };
        layout.Controls.Add(showGallery);
        Label("열린 캡처를 썸네일로 보고 클릭해 탭을 전환합니다.");
        var error = new Label { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(24, 5, 24, 0), ForeColor = Color.Firebrick };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(16, 10, 16, 10), FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.FromArgb(248, 250, 252) };
        var save = new ChromeButton { Text = "변경 저장", AccessibleName = "변경 저장", Primary = true, Width = 110, Height = 36 };
        var recheck = new ChromeButton { Text = "다시 확인", AccessibleName = "다시 확인", Width = 110, Height = 34, Margin = new Padding(0, 12, 0, 0) };
        shortcutsPage.Controls.Add(recheck);
        var cancel = new ChromeButton { Text = "취소", Width = 90, Height = 36, DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([cancel, save]); Controls.Add(error); Controls.Add(actions);
        actions.BringToFront(); error.BringToFront(); body.BringToFront();
        CancelButton = cancel;
        bool CheckHotkeys()
        {
            bool duplicate = region.Input.SelectedKeys == full.Input.SelectedKeys;
            bool available = true;
            foreach (var (input, status) in new[] { region, full })
            {
                var result = duplicate ? new HotkeyAvailability(false, "영역·전체 화면 단축키가 같습니다.") : checkHotkey(input.SelectedKeys);
                status.Text = result.Message; status.ForeColor = result.Available ? Color.FromArgb(21, 128, 61) : Color.Firebrick;
                available &= result.Available;
            }
            const string unavailable = "단축키를 사용할 수 없습니다. 단축키 탭에서 확인하세요.";
            if (!available) error.Text = unavailable;
            else if (error.Text == unavailable) error.Text = "";
            save.Enabled = available; return available;
        }
        region.Input.SelectionChanged += (_, _) => { error.Text = ""; CheckHotkeys(); };
        full.Input.SelectionChanged += (_, _) => { error.Text = ""; CheckHotkeys(); };
        recheck.Click += (_, _) => CheckHotkeys();
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, area.Width - 24), Math.Min(Height, area.Height - 24));
            Location = new Point(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
            CheckHotkeys();
        };
        save.Click += (_, _) =>
        {
            if (!CheckHotkeys()) { tabs.SelectedIndex = 1; return; }
            string saveFolder = folder.Text.Trim();
            if (autoSave.Checked)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(saveFolder) || !Path.IsPathFullyQualified(saveFolder))
                        throw new ArgumentException("자동 저장 폴더의 전체 경로를 지정하세요.");
                    saveFolder = Path.GetFullPath(saveFolder);
                    Directory.CreateDirectory(saveFolder);
                }
                catch (Exception e) { tabs.SelectedIndex = 2; error.Text = "저장 폴더 확인 실패: " + e.Message; return; }
            }
            var updated = new AppSettings
            {
                RegionHotkey = region.Input.SelectedKeys, FullscreenHotkey = full.Input.SelectedKeys,
                NewCaptureSelectRegion = mode.SelectedIndex == 0, AutoCopy = autoCopy.Checked, ShowGallery = showGallery.Checked,
                AutoSave = autoSave.Checked, AutoSaveFolder = saveFolder
            };
            if (updated.RegionHotkey == updated.FullscreenHotkey) { error.Text = "두 단축키는 다르게 지정하세요."; return; }
            if (runAtLogin.HasValue && startup.Checked != runAtLogin.Value)
            {
                try { setRunAtLogin?.Invoke(startup.Checked); }
                catch (Exception e) { error.Text = "자동 실행 설정 실패: " + e.Message; tabs.SelectedIndex = 3; return; }
            }
            error.Text = apply(updated) ?? "";
            if (error.Text.Length != 0 && runAtLogin.HasValue && startup.Checked != runAtLogin.Value)
            { try { setRunAtLogin?.Invoke(runAtLogin.Value); } catch (Exception e) { error.Text += " · 자동 실행 복원 실패: " + e.Message; } }
            if (error.Text.Length == 0) { DialogResult = DialogResult.OK; Close(); }
        };
    }
}
