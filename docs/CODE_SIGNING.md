# Windows 배포와 SmartScreen

현재 1.0.1 설치 파일은 디지털 서명이 없습니다. SmartScreen의 인식할 수 없는 앱 경고는 악성코드 탐지와는 별도로 파일·게시자의 평판 부족으로 표시될 수 있습니다. 서명만으로 경고가 즉시 사라진다고 보장할 수 없습니다.

## 서명 빌드

공인 코드 서명 인증서와 개인 키를 Windows 인증서 저장소의 개인(My)에 준비합니다. 자체 서명 인증서는 일반 사용자의 신뢰 문제를 해결하지 못합니다. Windows SDK의 x64 SignTool도 필요합니다. 인증서 구매·신원 확인은 별도로 진행해야 합니다.

```powershell
Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Select-Object Subject, Thumbprint, NotAfter, HasPrivateKey

.\build-installer.ps1 -CertificateThumbprint '<인증서의 40자리 지문>'
```

LocalMachine 저장소를 사용하는 경우 `-MachineCertificateStore`를 추가합니다. SignTool 경로는 `-SignTool`로 지정할 수 있습니다. 인증서 지문은 `ASNAPDO_SIGNING_THUMBPRINT` 환경 변수로도 전달할 수 있습니다. 비밀번호와 개인 키는 저장소에 넣지 마세요.

빌드는 인증서와 도구를 먼저 검사한 뒤 네이티브 실행 파일, 편집기 EXE 및 앱 DLL에 SHA-256 서명과 RFC 3161 타임스탬프를 적용합니다. 검증에 성공한 파일을 설치 프로그램에 포함하고 마지막으로 설치 프로그램 자체에 서명합니다. 서명·타임스탬프·검증이 실패하면 빌드를 중단합니다. Inno Setup이 설치 중 생성하는 제거 프로그램의 서명은 이 단계에 포함하지 않습니다.

인증서를 지정하지 않으면 경고를 표시하고 기존의 서명 없는 개발 빌드를 생성합니다. 공개 배포는 서명 옵션을 지정해 수행하세요. 이미 빌드한 파일을 나중에 수정하면 서명이 무효화됩니다.

## 배포 파일 확인

```powershell
Get-AuthenticodeSignature .\release\setup\asnapdo_inst_x64_1.0.1.exe |
    Format-List Status, SignerCertificate, TimeStamperCertificate
Get-FileHash .\release\setup\asnapdo_inst_x64_1.0.1.exe -Algorithm SHA256
```

서명은 파일 내용과 SHA-256을 변경합니다. 서명된 파일로 배포할 때 GitHub 릴리즈 첨부 파일과 웹사이트의 파일 크기·SHA-256도 함께 갱신해야 합니다. 기존에 받은 파일에는 새 서명이 적용되지 않습니다. Windows 보안 기능을 끄도록 안내하지 마세요.

공식 문서: https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation

## 이번 환경 확인

2026-10-31: CurrentUser 및 LocalMachine의 My 저장소에서 코드 서명 인증서를 찾지 못했습니다. Windows SDK SignTool은 설치되어 있습니다. 실제 공인 서명 및 SmartScreen 경고 해소는 미완료입니다.

## SignPath Foundation

2026-10-31: 사용자의 승인으로 프로젝트 소스와 문서를 MIT 라이선스로 전환했습니다.
SignPath Foundation 신청은 아직 제출하지 않았으며, 프로젝트 승인·인증서·서명 연동은 미완료입니다.
기존 GitHub Release의 설치 파일과 서명 상태는 변경하지 않았습니다.

### 신청 준비

1. MIT 라이선스가 포함된 소스를 공개 저장소 https://github.com/mant81/ASnapdo 에 반영합니다.
2. 소스와 포함된 구성 요소가 모두 신청 조건에 부합하는지 확인합니다. ASnapdo의 MIT 라이선스는 제3자 구성 요소의 권리를 변경하지 않습니다.
3. https://signpath.org/apply 에서 소유자 계정으로 무료 OSS 구독을 신청합니다. 프로젝트 설명, 저장소·다운로드 주소, MIT 라이선스와 이 정책의 링크를 준비합니다.
4. 신청 시 개발자·검토자·서명 승인자를 지정하고 GitHub 및 SignPath 계정에 MFA를 설정합니다. 승인자는 저장소 소유자가 지정하며 아직 별도로 확정하지 않았습니다.
5. 승인 후 SignPath 조직 ID, 프로젝트 slug, 서명 정책 slug 및 artifact configuration을 설정합니다. API 토큰은 GitHub Actions secret `SIGNPATH_API_TOKEN`에 저장하고 채팅이나 소스에 넣지 않습니다.

### Code signing policy

승인 전에는 SignPath가 이 프로젝트에 무료 서명이나 인증서를 제공한다고 표시하지 않습니다.
승인된 경우 공식 요구 문구인 “Free code signing provided by SignPath.io, certificate by SignPath Foundation”과 확정된 팀 역할을 홈페이지·다운로드 페이지에도 반영합니다.

서명 대상은 자체 빌드한 `ASnapdo.exe`(네이티브 런처), `editor/ASnapdo.exe`, `editor/ASnapdo.dll` 및 최종 설치 파일입니다. 제3자 DLL은 ASnapdo 인증서로 서명하지 않습니다.
GitHub가 호스팅하는 Windows runner에서 소스로부터 빌드하고, GitHub Actions artifact를 SignPath에 제출하여 출처 검증을 받는 흐름을 사용합니다.
앱 파일 서명 → 서명 검증 → 해당 파일로 설치 프로그램 빌드 → 설치 프로그램 서명 → 검증 순서로 구성해야 합니다. 기존 `build-installer.ps1`은 앱을 다시 빌드하므로 SignPath에서 받은 서명 파일에 그대로 실행하면 안 됩니다.
각 서명 요청에는 지정된 승인자의 승인이 필요합니다. 서명 및 타임스탬프 검증에 실패한 파일은 서명된 배포본으로 게시하지 않습니다.
배포는 새 버전 번호를 사용하고 `publish-release.ps1`으로 설치 파일을 첨부한 GitHub Release를 생성합니다. 이미 게시된 버전의 바이너리는 교체하지 않습니다.

기존 `-CertificateThumbprint` 옵션은 Windows 인증서 저장소의 개인 키를 사용하는 별도 방식입니다. Foundation의 HSM 키를 로컬 저장소에 설치하는 방식으로 연동하지 않습니다.

공식 조건: https://signpath.org/terms
GitHub 연동: https://docs.signpath.io/trusted-build-systems/github

### MIT 전환 검증 (2026-10-31)

- `dotnet build src/ASnapdo/ASnapdo.csproj -c Release`: 성공, 경고 0 / 오류 0 (.NET SDK 10.0.403, Windows).
- `dotnet run --project tests/ASnapdo.Smoke -c Release`: 통과. 도움말이 내장된 MIT 원문과 동일하며 AAStudio.kr 저작권 표시를 포함하는지 확인. 실제 도움말 화면은 `docs/screenshots/asnapdo-help-0.png`에 생성.
- `dotnet run --project tests/ASnapdo.Smoke -c Release -- --security-test`: 통과.
- 설치 UI/설치 검사 스크립트의 기대 문구를 MIT로 변경했으며 이번 작업에서 설치 프로그램 재빌드·설치 검사는 실행하지 않음.
- 배포·기존 바이너리 교체·SignPath 신청 제출·공인 서명은 수행하지 않음.
