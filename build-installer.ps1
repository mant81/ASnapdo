param([string]$Compiler = "$PSScriptRoot\tools\vendor\InnoSetup\ISCC.exe")
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & "$PSScriptRoot\build-exe.cmd"
    if ($LASTEXITCODE -ne 0) { throw 'Editor publish failed.' }
    if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Inno Setup ISCC.exe is required. Pass -Compiler with its path.' }
    & $Compiler /Q installer/ASnapdo.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} finally { Pop-Location }
