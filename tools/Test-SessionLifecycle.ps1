param([string]$AppFolder = "$PSScriptRoot\..\release\app")
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SecurityLifecycleWindow {
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string title);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, int message, IntPtr w, IntPtr l);
}
'@
function Wait-For([scriptblock]$Condition) {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do { if (& $Condition) { return }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Timed out waiting for lifecycle state.'
}
if ([SecurityLifecycleWindow]::FindWindow('ASnapdo.Tray', 'ASnapdo') -ne [IntPtr]::Zero) { throw 'Exit the existing ASnapdo tray before this isolated test.' }
$AppFolder = [IO.Path]::GetFullPath($AppFolder)
$profile = Join-Path $AppFolder ('security-lifecycle-' + [Guid]::NewGuid().ToString('N'))
$oldProfile = $env:ASNAPDO_DATA_DIR
$env:ASNAPDO_DATA_DIR = $profile
$trayProcess = $null
try {
    & dotnet run --project "$PSScriptRoot\..\tests\ASnapdo.Smoke\ASnapdo.Smoke.csproj" -c Release --no-build -- --session-fixture "$profile\session.json"
    if ($LASTEXITCODE -ne 0) { throw 'Fixture creation failed.' }
    $trayProcess = Start-Process "$AppFolder\ASnapdo.exe" -ArgumentList '--background' -WindowStyle Hidden -PassThru
    Wait-For { [SecurityLifecycleWindow]::FindWindow('ASnapdo.Tray', 'ASnapdo') -ne [IntPtr]::Zero }
    $window = [SecurityLifecycleWindow]::FindWindow('ASnapdo.Tray', 'ASnapdo')
    foreach ($cycle in 1..2) {
        [void][SecurityLifecycleWindow]::PostMessage($window, 0x8001, [IntPtr]::Zero, [IntPtr]::Zero)
        Wait-For { @(Get-Process ASnapdo -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$AppFolder\editor\ASnapdo.exe" -and $_.MainWindowHandle -ne [IntPtr]::Zero }).Count -eq 1 }
        $editorProcess = Get-Process ASnapdo | Where-Object { $_.Path -eq "$AppFolder\editor\ASnapdo.exe" }
        Start-Sleep -Seconds 1
        [void][SecurityLifecycleWindow]::PostMessage($editorProcess.MainWindowHandle, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
        Wait-For { $editorProcess.Refresh(); $editorProcess.HasExited }
        & dotnet run --project "$PSScriptRoot\..\tests\ASnapdo.Smoke\ASnapdo.Smoke.csproj" -c Release --no-build -- --session-verify "$profile\session.json"
        if ($LASTEXITCODE -ne 0) { throw 'Encrypted session verification failed.' }
    }
    [void][SecurityLifecycleWindow]::PostMessage($window, 0x8002, [IntPtr]::Zero, [IntPtr]::Zero)
    Wait-For { $trayProcess.Refresh(); $trayProcess.HasExited }
    if (Test-Path -LiteralPath "$profile\session.json") { throw 'Tray exit left the saved session.' }
    Write-Output 'PASS: two encrypted session open/close cycles and deletion on tray exit. Memory budget is tested separately by Test-Resident.ps1.'
} finally {
    if ($trayProcess -and -not $trayProcess.HasExited) { Stop-Process -Id $trayProcess.Id }
    Get-Process ASnapdo -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$AppFolder\editor\ASnapdo.exe" } | Stop-Process
    $env:ASNAPDO_DATA_DIR = $oldProfile
}
