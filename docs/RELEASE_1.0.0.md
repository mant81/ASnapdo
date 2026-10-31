# ASnapdo 1.0.0 릴리즈 — 2026-10-31

## 산출물

- 버전: 편집기 어셈블리, 네이티브 EXE 버전 정보 및 매니페스트, 설치 프로그램 모두 1.0.0.
- 설치 파일: `release/setup/asnapdo_inst_x64_1.0.0.exe`.
- 제작사: AAStudio.kr. 개인·기업·관공서 무료.
- 기본 설치 폴더: `C:\Program Files\ASnapdo`. 일반 설치 패키지는 관리자 권한 필요. 자동 실행 작업은 원래 설치를 시작한 사용자 권한으로 HKCU에 등록하는 네이티브 명령 사용.
- 웹 다운로드 복사본: `D:\ai\aastudio.kr\release\asnapdo_inst_x64_1.0.0.exe`.
- 웹 릴리즈 메타데이터: `D:\ai\aastudio.kr\release\asnapdo-1.0.0.json`. 웹 복사본과 원본 설치 파일의 SHA-256 일치 확인.

## 웹 변경

- 기존 정적 HTML 사이트에 `/asnapdo/` 제품 설명, `/asnapdo/changelog/` 변경이력 및 홈 제품 영역 추가.
- 자체 아이콘과 실제 앱 UI(합성 예시 캡처)를 사용. 다른 제품명이나 자산을 추가하지 않음.
- 모든 ASnapdo 다운로드는 사이트의 `release/` 파일로 직접 연결. 크기·SHA-256 및 무료 이용약관 제공.
- Pages 워크플로의 공개 파일 준비 단계에 `asnapdo/`, `release/` 복사 추가. 기존 사이트의 다른 도구와 브랜드 영역은 유지.
- 사이트 README와 `docs/asnapdo-product.md`에 운영·업데이트 방법과 검증 범위 기록.

## 수행한 검증

- `build-installer.ps1`: 성공. NuGet 취약성 피드 연결 실패 NU1900 경고 있음.
- `dotnet run --project tests/ASnapdo.Smoke/ASnapdo.Smoke.csproj -c Release --no-restore`: 기존 기능 테스트 통과.
- `tools/Test-Installer.ps1` 및 `tools/Test-LicenseUI.ps1`: 별도 테스트 AppId/사용자 권한 테스트 패키지로 설치·재설치·제거, EXE/DLL·약관·제작사·자동 실행 항목, 동의 전후 다음 버튼 상태 확인. 실제 관리자 승인을 받아 Program Files에 설치하는 실기 검증은 수행하지 않음.
- `tools/Test-Resident.ps1`: 네이티브 프로세스 1개, 최초 대기 작업 집합 8.48MiB, 편집기 열기/닫기 두 번 후 8.65MiB, 전용 커밋 1.36MiB. 각각 10,000,000바이트 미만.
- `tools/Verify-ProductSite.cjs`: 실제 사이트 저장소를 로컬 HTTP 서버로 열어 홈·설명·변경이력의 1440/900/390px 화면, 가로 넘침 없음, 내부 링크 16개, 이미지 로딩, JavaScript 예외 없음 확인. 브라우저에서 설치 파일을 다운로드해 파일명·크기·SHA-256 확인.
- 사이트 스크린샷: `docs/screenshots/site/`.

## 남은 배포 작업과 검증 범위

GitHub v1.0.0 릴리즈에는 설치 EXE만 배포하며 소스·빌드 스크립트·문서는 main에서 관리합니다. Pages 공개 배포는 이번 작업에 포함하지 않습니다. DNS 및 공개 도메인 다운로드, 실제 Windows 재로그인, 다른 Windows 환경과 UAC의 별도 관리자 계정 입력은 실기 검증하지 않음. 코드 서명은 미구성.

## 2026-10-31 설치 릴리즈

- 다운로드: https://github.com/mant81/ASnapdo/releases/tag/v1.0.0
- 설치 파일: asnapdo_inst_x64_1.0.0.exe (36,548,974 bytes)
- SHA-256: 238a889d7f893d052ac9ea341c2a264f989ef4b2e2bf0f7e3b31dffd405cde3d
- 이번 검증: Release 빌드 경고 0개·오류 0개, ASnapdo.Smoke 전체 통과, build-installer.ps1 성공(NU1900 경고).
- 빌드 결과, 외부 도구, 검증 스크린샷은 Git 커밋에서 제외합니다.
