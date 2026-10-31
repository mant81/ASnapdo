namespace ASnapdo.UI;

public sealed class HelpForm : Form
{
    public HelpForm()
    {
        Icon = AppIcon.Load(); Disposed += (_, _) => Icon?.Dispose();
        Text = "ASnapdo 도움말"; ClientSize = new Size(620, 480); MinimumSize = new Size(480, 360);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent; BackColor = Color.White;
        MaximizeBox = false; MinimizeBox = false;
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 7) };
        void Page(string title, string body)
        {
            var page = new TabPage(title) { Padding = new Padding(16), BackColor = Color.White };
            page.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = Color.White,
                ForeColor = Color.FromArgb(51, 65, 85), Text = body.ReplaceLineEndings(), AccessibleName = title });
            tabs.TabPages.Add(page);
        }
        Page("라이선스 정책", Services.LicenseInfo.Terms);
        Page("주요 기능", "캡처와 탭\n영역·전체 화면 캡처, 캡처별 독립 탭, 이미지 열기\n설정 가능한 전역 캡처 단축키\n\n편집\n화살표·사각형·텍스트·번호·형광펜\n객체 선택·이동·크기 조절·삭제, Ctrl+Z 실행 취소\n선택 영역 자르기, 이미지 기준 픽셀 줄자\n모자이크·블러: 영역 드래그 → 강도 조절 → Enter 적용 / Esc 취소\nCtrl+마우스 휠 확대·축소, Ctrl+0 화면 맞춤\n\n복사와 붙여넣기\nCtrl+C: 편집 결과 전체 이미지 복사\n영역 선택 → Enter: 영역 이미지 복사\nCtrl+V: 이미지를 현재 탭에 객체로 붙여넣기\nCtrl+Shift+C / Ctrl+Alt+C: 전체 / 선택 주석 복사\nCtrl+Shift+V: 주석 붙여넣기\n텍스트 입력 중에는 표준 텍스트 편집 단축키가 적용됩니다.\n\n저장과 트레이\nCtrl+S: 편집 결과 PNG 저장\n설정에서 캡처 원본 자동 저장 및 저장 폴더 지정\n설정 → 일반: Windows 로그인 시 자동 실행, 하단 캡처 갤러리 표시\n설치형의 창 X: 편집 상태 보관 후 편집기를 종료하고 트레이만 대기\n트레이 종료: 저장 확인 없이 종료");
        var version = typeof(HelpForm).Assembly.GetName().Version;
        Page("업데이트", $"현재 버전: {version?.ToString(3) ?? "확인 불가"}\n\n현재 자동 업데이트와 온라인 최신 버전 확인 기능은 제공하지 않습니다.\n\n새 설치 버전을 전달받으면\n1. 필요한 편집 결과를 Ctrl+S로 저장합니다.\n2. 트레이 메뉴의 종료로 실행 중인 ASnapdo를 종료합니다.\n3. 새 버전의 설치 파일을 실행해 업데이트합니다.\n\n설정은 사용자 계정의 ASnapdo 설정 폴더에 보관됩니다.\n공식 배포 주소와 업데이트 채널은 아직 확정되지 않았습니다.");
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(10) };
        var close = new ChromeButton { Text = "닫기", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 90, Primary = true };
        bottom.Controls.Add(close); CancelButton = close;
        Shown += (_, _) => close.Focus();
        tabs.SelectedIndexChanged += (_, _) => close.Focus();
        Controls.Add(tabs); Controls.Add(bottom);
    }
}
