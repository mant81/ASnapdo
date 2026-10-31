param([string]$AppFolder = "$PSScriptRoot\..\release\app")
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ResidentWindow {
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string title);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, int message, IntPtr w, IntPtr l);
}
'@
function Wait-Until([scriptblock]$Condition) {
    $until = [DateTime]::UtcNow.AddSeconds(20)
    do { if (& $Condition) { return }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $until)
    throw 'Timed out waiting for process state.'
}
$AppFolder = [IO.Path]::GetFullPath($AppFolder)
$profileFolder = Join-Path $AppFolder 'resident-test-profile'
New-Item -ItemType Directory -Force -Path $profileFolder | Out-Null
$originalProfile = $env:ASNAPDO_DATA_DIR
$env:ASNAPDO_DATA_DIR = $profileFolder
$trayProcess = $null
try {
    if ([ResidentWindow]::FindWindow('ASnapdo.Tray', 'ASnapdo') -ne [IntPtr]::Zero) { throw 'Exit the running ASnapdo tray before this isolated test.' }
    & dotnet run --project "$PSScriptRoot\..\tests\ASnapdo.Smoke\ASnapdo.Smoke.csproj" -c Release --no-restore -- --session-fixture "$profileFolder\session.json"
    if ($LASTEXITCODE -ne 0) { throw 'Session fixture creation failed.' }
    '{"ShowGallery":true}' | Set-Content -LiteralPath "$profileFolder\settings.json" -Encoding UTF8
    $trayProcess = Start-Process -FilePath "$AppFolder\ASnapdo.exe" -ArgumentList '--background' -WindowStyle Hidden -PassThru
    Write-Output 'Waiting for native tray window...'
    Wait-Until { [ResidentWindow]::FindWindow('ASnapdo.Tray', 'ASnapdo') -ne [IntPtr]::Zero }
    $trayWindow = [ResidentWindow]::FindWindow('ASnapdo.Tray', 'ASnapdo')
    $measurements = @()
    foreach ($cycle in 0..2) {
        if ($cycle -gt 0) {
            Write-Output "Opening editor, cycle $cycle..."
            [void][ResidentWindow]::PostMessage($trayWindow, 0x8001, [IntPtr]::Zero, [IntPtr]::Zero)
            Wait-Until { @(Get-Process ASnapdo -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$AppFolder\editor\ASnapdo.exe" -and $_.MainWindowHandle -ne [IntPtr]::Zero }).Count -eq 1 }
            $editor = Get-Process ASnapdo | Where-Object { $_.Path -eq "$AppFolder\editor\ASnapdo.exe" }
            Write-Output "Closing editor PID $($editor.Id)..."
            Start-Sleep -Seconds 1
            [void][ResidentWindow]::PostMessage($editor.MainWindowHandle, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
            Wait-Until { $editor.Refresh(); $editor.HasExited }
            & dotnet run --project "$PSScriptRoot\..\tests\ASnapdo.Smoke\ASnapdo.Smoke.csproj" -c Release --no-build -- --session-verify "$profileFolder\session.json"
            if ($LASTEXITCODE -ne 0) { throw 'Encrypted session verification failed.' }
        }
        Start-Sleep -Seconds 2
        $related = @(Get-Process ASnapdo -ErrorAction SilentlyContinue | Where-Object { $_.Path.StartsWith($AppFolder, [StringComparison]::OrdinalIgnoreCase) })
        $working = ($related | Measure-Object WorkingSet64 -Sum).Sum
        $private = ($related | Measure-Object PrivateMemorySize64 -Sum).Sum
        $measurements += [pscustomobject]@{ Cycle=$cycle; Processes=$related.Count; WorkingSetMB=[math]::Round($working/1MB,2); PrivateMB=[math]::Round($private/1MB,2) }
        if ($related.Count -ne 1 -or $working -ge 10000000 -or $private -ge 10000000) { throw "Idle memory exceeds limit: $working bytes" }
    }
    $measurements | Format-Table | Out-String | Write-Output
    $measurements | ConvertTo-Json | Set-Content -LiteralPath "$AppFolder\resident-memory-results.json" -Encoding UTF8
    # Exit immediately during editor startup as well as from the idle state.
    [void][ResidentWindow]::PostMessage($trayWindow, 0x8001, [IntPtr]::Zero, [IntPtr]::Zero)
    [void][ResidentWindow]::PostMessage($trayWindow, 0x8002, [IntPtr]::Zero, [IntPtr]::Zero)
    Wait-Until { $trayProcess.Refresh(); $trayProcess.HasExited }
    if (Test-Path -LiteralPath "$profileFolder\session.json") { throw 'Tray Exit did not discard session.' }
    Write-Output 'PASS: native-only idle below 10,000,000 bytes, two editor open/close cycles, session preservation and exit during editor startup.'
} finally {
    if ($trayProcess -and -not $trayProcess.HasExited) { Stop-Process -Id $trayProcess.Id }
    Get-Process ASnapdo -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$AppFolder\editor\ASnapdo.exe" } | Stop-Process
    $env:ASNAPDO_DATA_DIR = $originalProfile
}
