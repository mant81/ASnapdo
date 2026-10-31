param([string]$Package = "")
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..")
if (-not $Package) { $Package = "$root\release\install-smoke-package\ASnapdo-Setup-Smoke.exe" }
$destination = "$root\release\install-smoke-app"
$log = "$root\release\install-smoke-package\install.log"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Test-Path -LiteralPath $destination) { throw 'Smoke install destination already exists; inspect it before retrying.' }
try {
    foreach ($round in 1..2) {
        $setup = Start-Process -FilePath $package -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/TASKS=startup',"/DIR=$destination","/LOG=$log" -WindowStyle Hidden -PassThru -Wait
        if ($setup.ExitCode -ne 0) { throw "Installer returned $($setup.ExitCode)" }
        if (-not (Test-Path -LiteralPath "$destination\ASnapdo.exe") -or -not (Test-Path -LiteralPath "$destination\editor\ASnapdo.dll")) { throw 'Installed launcher/editor files are missing.' }
        $license = Get-Content -LiteralPath "$destination\LICENSE.txt" -Raw -Encoding UTF8
        if (-not $license.Contains('개인·기업·관공서 모두 무료') -or -not $license.Contains('AAStudio.kr')) { throw 'Installed license is missing the free-use policy or publisher.' }
        if ([Diagnostics.FileVersionInfo]::GetVersionInfo("$destination\ASnapdo.exe").CompanyName -ne 'AAStudio.kr') { throw 'Launcher publisher is incorrect.' }
        if (Get-ChildItem -LiteralPath $destination -Filter '*.pdb' -Recurse) { throw 'Debug symbols were installed.' }
        $startup = (Get-ItemProperty -LiteralPath $runKey).'ASnapdo-InstallSmoke'
        if ($startup -ne "`"$destination\ASnapdo.exe`" --background") { throw 'Startup command is incorrect.' }
        Write-Output "PASS: install round $round, separate EXE/DLL, optional startup registration, no PDB."
    }
} finally {
    if (Test-Path -LiteralPath "$destination\unins000.exe") {
        $uninstall = Start-Process -FilePath "$destination\unins000.exe" -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -PassThru -Wait
        if ($uninstall.ExitCode -ne 0) { throw "Uninstaller returned $($uninstall.ExitCode)" }
        $until = [DateTime]::UtcNow.AddSeconds(10)
        while ((Test-Path -LiteralPath "$destination\ASnapdo.exe") -and [DateTime]::UtcNow -lt $until) { Start-Sleep -Milliseconds 100 }
        if (Test-Path -LiteralPath "$destination\ASnapdo.exe") { throw 'Uninstall left the launcher.' }
        if ((Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).'ASnapdo-InstallSmoke') { throw 'Uninstall left startup registration.' }
        Write-Output 'PASS: uninstall removed program and test startup entry.'
    }
}
