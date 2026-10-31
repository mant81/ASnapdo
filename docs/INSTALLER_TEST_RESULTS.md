# 설치형 및 최소 상주 검증 — 2026-10-31

환경: Windows 10.0.26300 x64, .NET SDK 10.0.403, MSVC 14.44, Inno Setup 6.7.4.

## 구조

- 네이티브 `ASnapdo.exe`: 20,480바이트. CRT/.NET을 로드하지 않는 Win32 트레이·전역 단축키·편집기 실행 담당.
- `editor/ASnapdo.exe`: 174,080바이트. 코드·프레임워크·런타임은 DLL로 분리.
- 사용자별 설치, 시작 메뉴, 선택적 바탕 화면 바로가기/로그인 자동 실행, 제거 프로그램 제공.
- 설치형 창 X는 세션 보관 후 .NET 프로세스 종료. 다시 열면 탭과 Undo 복원. 트레이 Exit는 저장 확인 없이 세션 폐기.
- 기존 `build-exe.cmd`의 단일 EXE 배포는 분리 배포로 변경. 설치 배포는 `build-installer.ps1`을 사용.

## 실제 검증

`dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release --no-restore`

기존 캡처 선택·편집·탭·복사·자르기·Undo·설정·단축키 테스트 통과. 세션 직렬화에서 이미지, 주석 색상/좌표, 배율/스크롤, 이미지 및 주석 Undo 복원 확인.

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Resident.ps1`

| 대기 상태 | 프로세스 수 | 합계 작업 집합(MiB) | 합계 전용 커밋(MiB) |
| --- | ---: | ---: | ---: |
| 최초 백그라운드 시작 | 1 | 8.25 | 1.34 |
| 편집기 열기/닫기 1회 후 | 1 | 8.53 | 1.37 |
| 편집기 열기/닫기 2회 후 | 1 | 8.53 | 1.37 |

실제 Windows 프로세스 값을 측정했으며 작업 집합 강제 비우기는 사용하지 않음. .NET 편집기 프로세스가 남지 않는지 확인. 원본 탭/주석과 세션 재보관, 편집기 시작 직후 트레이 Exit를 눌러도 편집기와 트레이가 모두 종료되고 세션이 삭제되는지 확인. 테스트 합격 조건은 작업 집합 및 전용 커밋 합계 각각 10,000,000바이트 미만. 별도 데이터 경로로 사용자 세션을 건드리지 않고 검사.

설치 검증은 `/DInstallSmoke`로 별도 AppId와 별도 로그인 자동 실행 레지스트리 이름을 사용하는 패키지를 생성하여 `tools/Test-Installer.ps1`로 수행. 테스트 폴더에 설치 및 같은 경로 재설치, EXE/DLL 분리, PDB 제외, 로그인 자동 실행 명령을 확인. 제거 후 프로그램과 테스트 자동 실행 레지스트리 값이 제거되는 것을 확인. 설정의 자동 실행 적용 및 설정 저장 실패 시 등록 복원 테스트도 통과.

## 검증 범위 및 배포 전 잔여 사항

- 측정치는 현재 환경의 결과이며 다른 Windows 버전/보안 소프트웨어 환경의 사용량은 별도 검증 필요.
- 실제 Windows 로그아웃/로그인, 탐색기 재시작, 다중 모니터, 150%/200% DPI는 이 작업에서 실기 검증하지 않음.
- 코드 서명과 공식 업데이트 채널은 미구성. 설치 파일 생성은 공개 배포/업로드를 의미하지 않음.
- NuGet 취약성 피드 연결 실패로 NU1900 경고가 발생했으나 빌드/게시 자체는 성공.
- `ASNAPDO_DATA_DIR` 환경 변수는 테스트/격리 프로필을 위한 데이터 경로 오버라이드이며 기본 사용자 데이터 폴더는 유지됨.

## 모자이크·블러·갤러리 추가 후 재검증 — 2026-10-31

갤러리 표시 설정과 블러가 포함된 탭 세션으로 `tools/Test-Resident.ps1` 재실행 통과. 최초 대기 작업 집합 8.23MiB/전용 커밋 1.31MiB, 열기·닫기 두 번 후 8.51MiB/1.34MiB. 대기 프로세스는 1개이며 각각 10,000,000바이트 미만. 블러 래스터 편집의 세션 유지 확인. 설치 파일을 다시 생성했으며, 이번 기능 추가 후 설치·제거 실기 테스트는 반복하지 않았음.
## 무료 이용약관·제작사 표기 — 2026-10-31

- `LICENSE.txt` 추가: 개인·기업·관공서 모두 무료, 업무/영리 목적 사용 무료를 명시. 제작사 AAStudio.kr. 일반적인 사용 허용, 원본 무료 재배포, 이용 책임, 제공 상태, 제3자 구성 요소, 업데이트 안내 포함.
- 설치 프로그램의 무료 이용약관 단계와 앱 도움말이 동일한 약관 원문을 사용. 설치 후 앱 폴더에 LICENSE.txt 포함. 제작사는 설치 프로그램·네이티브 상주 EXE·편집기 EXE 회사 정보에 AAStudio.kr로 반영. 앱 상단 아이콘 + ASnapdo 표시는 유지.
- `dotnet build src/ASnapdo/ASnapdo.csproj -c Release`: 성공(경고 0/오류 0). 기존 자동 테스트 및 도움말 약관 원문 일치/무료 대상/제작사 검사 통과.
- `tools/Test-LicenseUI.ps1`: 실제 설치 마법사의 약관 내용과 무료 안내 확인. 동의 전 다음 버튼 비활성, 동의 후 활성 확인. 이 UI 검사는 설치 단계로 진행하지 않음. 실제 화면: docs/screenshots/asnapdo-installer-license.png.
- 별도 테스트 AppId 패키지로 `tools/Test-Installer.ps1 -Package release/license-install-smoke-package/ASnapdo-Setup-Smoke.exe`: 설치/재설치/제거 통과. 설치된 약관의 무료 대상/제작사, EXE 회사 정보, EXE/DLL 분리, PDB 제외, 테스트 자동 실행 등록 및 제거 확인.
- `build-installer.ps1` 및 갱신된 약관 헤더의 Inno Setup 재컴파일 성공. 최종 파일: release/setup/ASnapdo-Setup.exe. 설치 EXE의 CompanyName도 AAStudio.kr 확인. 코드 서명은 추가하지 않음.