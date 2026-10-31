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
