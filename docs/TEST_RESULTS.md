# 검증 기록 — 2026-10-30

## 초기 상태와 구현

초기 폴더에는 문서 4개만 있었습니다. 기존 v0.8 소스, Git 저장소, 디자인 PNG는 없었습니다. 기존 기능 검증 및 이름 마이그레이션 완료를 주장하지 않습니다.

.NET 10 WinForms 개발 기반을 신규 추가했습니다. 문서·주석·캡처·클립보드·전역 단축키·설정을 분리했습니다. 기존 소스 입수 시 대조가 필요합니다. WPF 전환 없이 명세의 WinForms 기술을 사용해 UI 스택 복잡도를 줄였습니다.

## 실제 검증 결과

Windows x64 환경. .NET SDK 10.0.403 및 .NET 10 Desktop Runtime 10.0.14 확인.

| 명령 | 결과 |
| --- | --- |
| `dotnet --version` / `dotnet --list-sdks` | `10.0.403` 확인 |
| `dotnet build src/ASnapdo/ASnapdo.csproj -c Release` | 성공, 경고 0 / 오류 0 |
| `cmd /c build-exe.cmd` | 종료 코드 1, 첫 실행은 NuGet 네트워크 제한으로 실패. 승인된 네트워크 재시도에서 게시 성공 |
| 소스 이름 검색 | ASnapdo 어셈블리 및 네임스페이스 확인. ACapdo는 설정 이관 경로에만 존재 |

Release 컴파일 및 단일 파일 게시에 성공했습니다. `release/win-x64/ASnapdo.exe`가 생성됐습니다. 초기 화면을 실제 실행해 캡처했습니다: `docs/screenshots/asnapdo-current.png`. 캡처 및 클립보드 편집 상호작용은 별도 미검증입니다. P0/P1/P2 수동 항목은 통과로 표시하지 않습니다.

## 추가 실행 검증

1. Release 빌드 및 게시, EXE 실행, 시작 예외 로그 확인.
2. 백그라운드 전역 단축키, 충돌 안내 및 기존 키 복원, 설정 재시작 유지.
3. 캡처 3개 이상 생성, 같은 초 이름 충돌, 탭별 Undo/선택/줌, 변경 탭 저장/취소.
4. 합성 이미지의 그림판/메신저 붙여넣기, 텍스트 입력 중 Ctrl+C 유지.
5. 다른 크기 이미지로 주석 복사, 참조 독립성 및 한 번의 Undo 확인.
6. 포인터 중심 확대, 객체 이동/크기 변경, 경계 좌표 및 반복 사용 GDI 리소스 확인.
7. 트레이 숨김/복원/종료, 다중 모니터 음수 좌표 및 혼합 DPI.
8. 1366×768/1920×1080/4K 및 125/150/200% DPI, 실제 화면 캡처 기록.

## 남은 작업과 제한

- 모자이크·블러·자르기, 픽셀 Undo, 원본 자동 저장, 선택형 갤러리 미구현.
- 화살표는 왼쪽 위→오른쪽 아래 방향 기반 구현. 임의 방향 끝점 모델 필요.
- 번호는 텍스트 기반. 스티커 모양 및 삭제 후 순번 정책 보강 필요.
- Undo는 주석 스냅샷이며 메모리 상한이 없습니다. 선택 클릭도 체크포인트를 만듭니다.
- 경계 상자 hit test. 텍스트 재편집 및 키보드 객체 이동 미구현.
- 도구 선택과 선택 객체의 색상·두께는 UI에 반영되지만 다양한 편집·Undo 조합의 추가 검증이 필요합니다.
- 수동 저장은 최초 파일 선택창에서 덮어쓰기 확인, 이후 Ctrl+S는 기존 경로 갱신.
- 기존 설정 스키마 미제공으로 호환성 미검증. ACapdo 원본은 수정하지 않고 새 경로에 복사하며 설정 저장 전 .bak 보존.
- 단축키 두 개를 서로 교환하는 설정은 기존 키 충돌로 거부될 수 있습니다.
- 캡처 중 종료, 시스템 종료 경로 및 DPI 동작은 런타임 검증 필요.
- 제품 아이콘 및 디자인 참조가 없어 시스템 기본 아이콘 사용.




## UI 개편 — 2026-10-30

UI 피드백에 따라 브랜드 헤더, 주 작업 툴바, 캡처와 주석 도구를 구분한 좌측 레일, 문서 탭, 우측 선택 객체 속성, 하단 줌/상태 표시로 정보 위계를 정리했습니다. ASnapdo의 이미지 탭·도구 모음·캔버스·상태 표시를 중심으로 작업 흐름을 구성하고 상단 작업줄과 좌우 속성 패널을 적용했습니다.

UI 수정 후 SDK 10.0.403으로 `dotnet build src/ASnapdo/ASnapdo.csproj -c Release`를 재실행했습니다: 성공, 경고 0 / 오류 0. `cmd /c build-exe.cmd` 게시도 성공해 `release/win-x64/ASnapdo.exe`를 갱신했습니다. UI를 실제 실행해 확인한 스크린샷은 아직 없습니다.

## 참조 구성 반영 — 2026-10-30

사용자가 제공한 화면 구성을 기준으로 상단 명령을 한 줄로 배치하고, 작업 공간을 좌측 도구/중앙 탭·캔버스/우측 속성 3열로 조정했습니다. 좌측 도구와 속성 패널을 넓히고, 캡처 전 중앙 시작 화면, 색상 팔레트, 상단 확대 컨트롤을 추가했습니다. 브랜드와 실제 구현된 기능은 ASnapdo 상태에 맞게 표시합니다.

반영 후 .NET SDK 10.0.403 Release 빌드 성공(경고 0 / 오류 0), 단일 파일 게시 성공. GUI에서의 배치/DPI 확인과 실행 스크린샷은 아직 미실시입니다.

## UI 수정 재검증 — 2026-10-30

사용자 참조 화면에 맞춰 단일 상단 명령줄과 좌측 도구/중앙 캔버스/우측 속성 구성을 적용했습니다. 화면 경계와 DPI 가상화 차이로 최대화 시 패널이 잘리던 문제를 확인해 첫 실행 창 크기를 화면에 맞추고, 레이아웃 영역을 직접 계산하도록 수정했습니다. 시작 화면 버튼은 일정한 높이로 정렬했습니다. 실제 실행 캡처는 `docs/screenshots/asnapdo-current.png`에 저장했습니다.

수정 후 SDK 10.0.403 빌드 성공(경고 0 / 오류 0), `build-exe.cmd` 게시 성공. 스크린샷은 시작 화면만 확인했습니다. 캡처 이후 탭 화면, DPI 배율별 레이아웃과 기능 동작은 추가 확인이 필요합니다.

## 캡처 편집 도구줄 및 문서 누적 — 2026-10-30

캡처 문서를 새 탭으로 추가하는 기존 동작을 유지하고, 활성 문서가 있을 때만 상단에 도구 리본을 표시하도록 했습니다. 선택·화살표·사각형·텍스트·번호·형광펜, 색상 팔레트, 선 두께, 확대/축소, 맞춤, 이미지 복사 기능을 그룹으로 배치했습니다. 좌측에는 영역/전체 화면 캡처를 남겨 중복 도구를 줄였습니다.

SDK 10.0.403 기준 Release 빌드(경고 0 / 오류 0) 및 `build-exe.cmd` 게시에 성공했습니다. 실제 다중 캡처 후 탭 전환/닫기 시나리오는 별도로 실행 검증하지 않았습니다.

## 디자인 전면 검토 — 2026-10-30

캡처 전/후 화면을 다시 구성해 상단 명령, 주석 도구, 문서 탭, 캔버스, 선택 객체 속성의 시각적 위계를 정리했습니다. 독자적인 벡터 아이콘과 선택·비활성 상태를 적용하고, 시작 화면과 좁은 창 배치를 조정했습니다. 좁은 창에서는 우측 패널을 숨기고 상단의 색상·두께와 하단 줌 컨트롤을 유지합니다. 이미지가 탭 표시 전에 잘못 축소되던 문제, 이미지 중앙 정렬, 선택 객체의 속성 표시, 확대 기준점 계산도 수정했습니다.

- `dotnet build src/ASnapdo/ASnapdo.csproj -c Release`: 성공, 경고 0 / 오류 0.
- `cmd /c build-exe.cmd`: 성공. NuGet 취약성 데이터 서버 접속 실패로 NU1900 경고가 표시됐으나 EXE는 생성됐습니다.
- Windows x64, .NET SDK 10.0.403, UI 검사 시 120 DPI(125%). WinForms 검사 프로그램에서 실제 `MainForm`에 합성 샘플 이미지 두 장을 추가했습니다. 서로 다른 두 탭, 다른 크기 이미지에 주석 복사와 한 번의 Undo, 선택 객체 속성, 이미지 기준점 확대/맞춤, 900×650 창 배치 확인에 성공했습니다.
- 화면 기록: `docs/screenshots/asnapdo-home-review.png`, `docs/screenshots/asnapdo-editor-review.png`, `docs/screenshots/asnapdo-editor-narrow.png`. 실제 프로그램 컨트롤을 렌더링한 화면이며, 편집 중인 이미지는 검증용 합성 이미지입니다.
- 실제 영역/전체 화면 캡처, 외부 앱 클립보드 붙여넣기, 저장 대화상자, dirty 닫기, 트레이 종료는 이 검토에서 재검증하지 않았습니다. 150%·200% 및 혼합 DPI도 미검증입니다.

## 전체 폭 캔버스와 편집 편의 — 2026-10-30

사용자 지시에 따라 좌우 패널을 제거했습니다. 기존 WinForms 엔진·탭·캡처 구조를 유지해 UI 기술 전환에 따른 회귀 위험을 줄였습니다. 상단 캡처 메뉴와 두 줄 도구/속성, 창 전체 폭의 탭과 캔버스 구성입니다. 좁은 창은 도구/명령을 툴팁이 있는 아이콘으로 축약합니다. 최대화 상태에서 캡처 후 복귀해도 최대화를 유지합니다.

수정 파일: MainForm.cs, UI/ChromeButton.cs, UI/EditorCanvas.cs, Models/Annotation.cs, Services/ClipboardService.cs, PRODUCT_SPEC.md, README.md. 회귀 검사 프로젝트 tests/ASnapdo.Smoke 추가.

- 버튼 모서리의 검은 잔상: 투명 부모색으로 지우던 렌더링을 불투명 조상 배경으로 지우도록 수정.
- 도형: 여덟 핸들, 채우기·점선, Shift 정사각형, 선택 클릭의 불필요한 Undo 제거, 객체 복제.
- 텍스트: 캔버스 안 여러 줄 입력, 더블클릭 재편집, 크기·굵게, Ctrl+Enter 확정 / Esc 취소. 빈 입력은 객체를 만들지 않음.
- 화살표: 드래그 방향 보존. 번호: 원형 스티커와 최대 번호 기준 증가.
- 내부 복사 시 글자 크기도 이미지 비율에 맞춰 변환.

Windows x64 / SDK 10.0.403 / 120 DPI(125%)에서 실행:

- `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release`: PASS. 실제 WinForms 컨트롤에 마우스 이벤트를 전달해 도형 생성, 여덟 핸들 각각의 크기 조절과 단일 Undo, 스타일 Undo, 객체 복제, 선택 클릭 Undo 오염 방지, 역방향 화살표, 여러 줄 텍스트 생성·더블클릭 수정·Undo·취소, 합성 이미지 크기를 검사. 문서 세 탭·중복 이름 방지·독립 주석 붙여넣기·Undo, 1280/900 너비의 전체 폭 캔버스도 검사.
- `dotnet build src/ASnapdo/ASnapdo.csproj -c Release --no-restore`: 오류 0. NU1900 경고 1(취약성 정보 서버 접근 실패).
- `cmd /c build-exe.cmd`: 성공. NU1900 경고 있음. `release/win-x64/ASnapdo.exe` 생성.
- UI 화면: `docs/screenshots/asnapdo-wide-canvas.png`, `docs/screenshots/asnapdo-wide-canvas-narrow.png`. 실제 MainForm 컨트롤을 DrawToBitmap으로 렌더링했으며 캔버스 내용은 합성 테스트 이미지. 운영체제 화면 캡처 결과나 디자인 시안이 아님.

미검증: 이번 변경 후 실제 전역 단축키 캡처·외부 앱 붙여넣기·dirty 닫기·저장 대화상자, 한국어 IME 실제 타이핑, Shift 키를 누른 수동 그리기, 150%/200% 및 혼합 DPI. 자동화는 입력 이벤트 호출 기반이며 OS 수준 마우스/키보드 종단 검사가 아님. 모자이크·블러·자르기 등 기존 미구현 항목은 그대로 남아 있음.

## 번호 수정과 캡처 옵션 — 2026-10-30

- 수정: UI/EditorCanvas.cs, Models/Annotation.cs, UI/OptionsForm.cs(신규), MainForm.cs, Services/SettingsService.cs, tests/ASnapdo.Smoke/Program.cs, README.md, PRODUCT_SPEC.md.
- 번호 생성 후 선택 도구로 자동 복귀하던 동작을 번호 도구에 한해 유지하도록 변경. 연속 클릭으로 번호 자동 증가. 기존 번호 더블클릭으로 1~9999 편집, Enter/포커스 이동 확정, Esc 취소, 수정 Undo 지원. 여러 자리 번호는 원 안에 맞춰 글자 축소.
- 옵션 창에 새 캡처 기본 동작(영역 선택/마우스가 있는 모니터 전체 화면), 영역/전체 단축키, 자동 복사 설정 배치. 새 캡처 버튼은 저장된 기본 동작을 즉시 실행; 우클릭으로 일회성 방식 선택. 기존 설정 파일에 새 필드가 없으면 영역 선택을 기본으로 함.
- 옵션은 저장 성공 후 메모리에 반영. 단축키 변경이 있을 때 등록을 교체하며 실패 시 이전 키 복원 시도. 캡처 방식만 변경하면 단축키를 재등록하지 않음.
- Windows x64 / SDK 10.0.403: `dotnet build src/ASnapdo/ASnapdo.csproj -c Release` 성공, 경고 0 / 오류 0.
- `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release` PASS. 번호 1·2 연속 생성, 더블클릭 수정, Undo/취소와 다음 번호 3, 옵션 UI 적용값, 기존 설정 기본값, JSON 직렬화 왕복 검사. 이전 편집/탭 검사도 통과. 실제 사용자 설정 파일은 테스트에서 변경하지 않음.
- 옵션 실제 컨트롤 렌더링: docs/screenshots/asnapdo-options.png (125% DPI).
- `cmd /c build-exe.cmd`: 기존 release/win-x64/ASnapdo.exe가 실행 중(PID 12816)이라 덮어쓰기 접근 거부로 실패. 앱은 종료하지 않음. 같은 게시 옵션에 `--no-restore -o release/number-options/win-x64`를 사용해 별도 게시 성공. 새 산출물은 **release/number-options/win-x64/ASnapdo.exe**. NU1900 취약성 서버 조회 경고 있음.
- 미검증: 새 옵션으로 실제 화면을 캡처하는 종단 시나리오, 전역 단축키 충돌/복원 및 앱 재시작 후 설정 로드의 수동 실행, 150%/200% DPI. 설정 저장 형식은 자동 검사했으나 사용자 설정 파일을 변경하는 테스트는 하지 않음.

## 단축키 선택 상자와 사용 가능 검사 — 2026-10-30

- 수정: UI/OptionsForm.cs, UI/HotkeySelector.cs(신규), Services/GlobalHotkeyService.cs, MainForm.cs, tests/ASnapdo.Smoke/Program.cs, README.md, PRODUCT_SPEC.md.
- 영역·전체 화면 각각 조합 키(Ctrl/Shift/Alt 및 조합)와 기본 키를 DropDownList로 선택. 기존 저장값은 목록에 보존. F12는 Windows 디버거 예약 키로 새 선택에서 제외.
- 선택 즉시 RegisterHotKey 임시 등록 후 해제. 앱이 이미 보유한 키는 사용 가능으로 인식하고 검사 중 기존 캡처 등록을 유지. 다른 키의 등록 실패는 OS 오류 번호와 함께 표시하며 점유 프로세스를 추정하지 않음.
- 두 동작에 동일한 키를 선택하거나 사용 불가이면 저장 비활성화. 다시 확인 버튼 제공. 저장 클릭 때 재검사하고 실제 적용 시 등록 결과도 확인하므로 검사 후 생긴 충돌도 처리.
- Windows x64 / SDK 10.0.403: `dotnet build src/ASnapdo/ASnapdo.csproj -c Release` 성공, 경고 0 / 오류 0.
- `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release` PASS. 실제 HWND 두 개로 RegisterHotKey 점유/가용/본인 등록 감지, 반복 검사 후 등록 해제, 기존 키 유지, 충돌 해제 후 재검사, 잘못된 키/F12 차단 검사. 옵션 UI 중복 저장 차단·선택 시 검사·저장 시 상태 변경 차단·재확인 복구도 검증. 이전 편집·번호·탭 검사 통과.
- 화면 `docs/screenshots/asnapdo-hotkey-options.png`: 실제 OptionsForm을 렌더링한 125% DPI 화면. 테스트용 Ctrl+Alt+Shift+F13/F14에 실제 Windows 검사를 실행한 결과이며 사용자 설정은 변경하지 않음.
- self-contained 단일 파일 `dotnet publish` 성공. `release/win-x64/ASnapdo.exe` 갱신 및 `release/hotkey-options/win-x64/ASnapdo.exe` 생성. NU1900(취약성 정보 서버 접속 실패) 경고 있음.
- 미검증: 실제 키 누름에 의한 전역 캡처 종단 동작, 사용자별 OS Print Screen 설정과의 상호 작용, 150%/200% DPI. 사용 가능 표시는 해당 시점 RegisterHotKey 결과이며 다른 프로그램의 후속 점유나 Windows 설정까지 보장하지 않음.

## 새 캡처 기본 100% — 2026-10-30

- MainForm.cs: 새 탭 초기화 때 자동 Fit 호출 제거. 문서 기본 배율 1.0으로 캔버스 크기를 원본 픽셀 크기에 맞추고 스크롤 시작점/정렬만 초기화. 비활성 탭도 초기화하며 기존 맞춤 버튼과 Ctrl+0은 유지. 이미지 열기도 같은 정책 적용.
- README.md / PRODUCT_SPEC.md에 정책 반영. tests/ASnapdo.Smoke/Program.cs에 새 문서 세 탭의 100% 및 캔버스 픽셀 크기, 명시적 Fit 동작, 좁은 창에서 배율 유지와 가로/세로 스크롤 검사 추가.
- Windows x64 / SDK 10.0.403: `dotnet build src/ASnapdo/ASnapdo.csproj -c Release` 성공(경고 0 / 오류 0). `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release` PASS(125% DPI, 합성 이미지 문서 기반). 기존 편집·번호·단축키 검사도 통과.
- `cmd /c build-exe.cmd` 성공, release/win-x64/ASnapdo.exe 갱신. NU1900 취약성 정보 조회 경고 있음.
- 실제 OS 캡처를 통한 종단 확인, 150%/200% 및 혼합 DPI는 이번 변경에서 미검증.

## 영역 캡처 가이드 — 2026-10-30

- 수정 파일: UI/SelectionForm.cs, MainForm.cs, tests/ASnapdo.Smoke/Program.cs, README.md, PRODUCT_SPEC.md.
- 영역 선택 시작 전 가상 데스크톱 원본을 고정. 선택 바깥 어둡게 표시, 내부 원본 밝기 유지, 십자/선택 경계 가이드 추가. 실시간 W×H px와 선택 시작 화면 좌표, 커서 주변 픽셀 확대 및 HEX 색상 표시. 정보창은 현재 모니터 안으로 제한하고 선택 영역과의 겹침을 최소화.
- Shift 정사각형, Esc/우클릭 취소, 빈 클릭 캡처 방지, 선택 끝점 화면 범위 제한. 드래그 완료 후 재촬영하지 않고 원본 스냅샷에서 선택 부분을 잘라 가이드가 결과에 포함되지 않게 함. 원본 Bitmap은 선택 폼 dispose 시 해제.
- Windows x64, SDK 10.0.403: `dotnet build src/ASnapdo/ASnapdo.csproj -c Release` 성공(경고 0 / 오류 0).
- `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release`: PASS. 합성 데스크톱으로 실제 SelectionForm 실행, 역방향 드래그 크기/절대 좌표, 결과 이미지 크기 및 원본 픽셀 대조, 음수 데스크톱 원점, 경계 밖 끝점 제한, 빈 클릭/우클릭 취소 검사. 기존 편집·번호·단축키·탭 검사도 통과.
- 실제 선택 폼 렌더링 화면: docs/screenshots/asnapdo-region-guides.png. 배경은 합성 테스트 이미지이며 사용자 데스크톱 캡처가 아님. 125% DPI에서 확인.
- `cmd /c build-exe.cmd` 성공. release/win-x64/ASnapdo.exe 갱신. NU1900 취약성 정보 서버 접근 경고 있음.
- 미검증: 실제 데스크톱에서 전역 단축키→선택→새 탭 종단 동작, Shift/Esc의 실제 키 입력, 서로 다른 DPI 다중 모니터. 음수 좌표 자동 검사는 합성 좌표 기반이며 실제 다중 모니터 검증을 대신하지 않음.

## 모자이크·블러·선택형 갤러리 — 2026-10-31

- 추가: `Services/PixelEffectService.cs`, `UI/CaptureGallery.cs`.
- 수정: `Models/Annotation.cs`, `Models/CaptureDocument.cs`, `Services/ClipboardService.cs`, `UI/EditorCanvas.cs`, `UI/EditorCanvas.Cropping.cs`, `UI/ChromeButton.cs`, `UI/OptionsForm.cs`, `UI/HelpForm.cs`, `MainForm.cs`, 테스트·메모리 검증 도구·제품 명세·README.
- 모자이크: 선택 영역의 블록 평균 색상, 블러: 알파를 고려한 분리형 박스 필터. 강도 2~64px. 역방향 드래그와 확대 배율에 맞는 영역 선택, 강도 미리보기, Enter/적용 확정, Esc/취소 지원.
- 합성 영역을 고정 래스터 편집으로 누적하여 배경과 겹친 주석 모두 처리하고, 영역 밖 객체는 계속 편집 가능. 래스터 편집은 직접 선택하거나 주석 버퍼로 복사되지 않으며 합성 이미지 복사·PNG·세션에 반영. 단일 Undo 지원.
- 갤러리 기본 숨김. 설정 → 일반에서 표시 선택 및 저장. 현재 열린 문서의 작은 썸네일만 생성하고 클릭·키보드로 탭 선택. 편집/Undo 후 갱신, 숨김/목록 변경/종료 시 썸네일 dispose.
- `dotnet build src/ASnapdo/ASnapdo.csproj -c Release`: 성공, 경고 0/오류 0.
- `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release --no-restore`: 통과. 블록 평균·블러 임펄스 응답·투명도·작은 영역·선택 경계 밖 불변·PNG 픽셀·주석 편집성·버퍼 제외·Undo·세션 복원·갤러리 기본값/설정 저장·썸네일 탭 전환/메모리 해제 검사. 기존 테스트도 통과.
- 실행 화면(합성 캡처를 편집하는 실제 WinForms UI): `docs/screenshots/asnapdo-gallery.png`, `docs/screenshots/asnapdo-blur-preview.png`. 125% DPI에서 확인. 150%/200% 및 실제 다중 모니터 실기 검증은 미실시.
- `powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1`: 성공. `release/setup/ASnapdo-Setup.exe` 갱신. 게시 시 NU1900 취약성 피드 연결 경고 있음.
- `tools/Test-Resident.ps1`: 갤러리 켜짐·블러 효과가 있는 세션으로 편집기 열기/닫기 2회 검사 통과. 대기 시 네이티브 프로세스 1개, 작업 집합 8.23~8.51MiB, 전용 커밋 1.31~1.34MiB. 각각 10,000,000바이트 미만. 편집기 시작 중 트레이 Exit도 종료 및 세션 폐기 정상.
## 2026-10-31 트레이 캡처 시작 시 창 깜빡임 수정

트레이에서 캡처를 위해 편집기를 시작할 때 최초 창 표시를 억제하고, 세션 복원 및 캡처 초기화를 숨겨진 상태에서 실행합니다. 캡처 결과를 추가한 뒤 편집 창을 표시합니다. 영역 선택을 취소하면 기존 세션 파일을 변경하지 않고 편집기를 종료해 네이티브 트레이 대기로 돌아갑니다. 실행 중인 편집기로 전달되는 캡처 요청도 먼저 창을 복원하지 않습니다.

검증: Windows / .NET SDK 10.0.403. `dotnet build src/ASnapdo/ASnapdo.csproj -c Release --no-restore` 성공(경고 0, 오류 0). `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release -- --capture-startup-test`에서 영역 캡처 시작 전 편집 창 미표시, 선택 취소 시 숨김 유지 및 종료, 전체 화면 캡처 완료 후 편집 창 표시를 확인했습니다. 실제 전역 단축키와 다중 모니터 조합의 수동 재검증은 미실시입니다.

## 2026-10-31 세션 보안 개선 검증

DPAPI 암호화·변조 거부·기존 평문 변환·기존 파일 보존·이미지 및 세션 자원 제한의 보안 회귀 검사, 전체 Smoke 및 캡처 시작 UI 검사 통과. 실제 트레이에서 암호화 세션 복원/저장 2회와 종료 삭제 통과. 별도 AppId의 설치·재설치·제거 검사 통과. 네이티브 PE의 보안 쿠키·CFG·ASLR·NX·CET 호환 플래그 확인. 상주 메모리 검사는 10,964,992바이트로 기존 10,000,000바이트 기준에서 실패했으며 검사 기준은 변경하지 않음. 상세 결과와 빌드 중 일시적 리소스 갱신 오류의 재시도 내용은 [SECURITY_REVIEW.md](SECURITY_REVIEW.md) 참고.

## ASnapdo 1.0.1 릴리즈 검증 — 2026-10-31

1.0.1 버전의 보안 회귀·전체 Smoke·캡처 시작·실제 트레이 암호화 세션 생명주기·긴 경로 검사 통과. 별도 AppId로 설치·재설치·제거 및 기본 체크 로그인 자동 실행 등록 통과. 설치 EXE에도 VersionInfoVersion/VersionInfoProductVersion을 명시했습니다. 설치 파일과 SHA-256은 [RELEASE_1.0.1.md](RELEASE_1.0.1.md)에 기록합니다. 앞서 확인한 미서명·백신 검사 미완료·대기 메모리 목표 초과는 그대로 남아 있습니다.
