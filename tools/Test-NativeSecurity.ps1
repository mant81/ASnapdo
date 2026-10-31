param([string]$Launcher = "$PSScriptRoot\..\release\app\ASnapdo.exe")
$ErrorActionPreference = 'Stop'
$previousFolder = $env:ASNAPDO_DATA_DIR
try {
    # Each case must reject long paths and ignore a startup-like unrecognized option.
    foreach ($length in @(1013, 1023, 1024, 4096)) {
        $env:ASNAPDO_DATA_DIR = 'C:\' + ('x' * ($length - 3))
        $process = Start-Process -FilePath $Launcher -ArgumentList '--background','--enable-startup-ignored' -WindowStyle Hidden -PassThru
        try {
            if (-not $process.WaitForExit(5000)) { throw "Launcher did not reject data path length $length." }
            if ($process.ExitCode -ne 111) { throw "Expected ERROR_BUFFER_OVERFLOW (111), got $($process.ExitCode) for length $length. Exit an existing ASnapdo tray before testing." }
            Write-Output "PASS: data path length $length rejected safely (111)."
        } finally {
            if (-not $process.HasExited) { Stop-Process -Id $process.Id }
            $process.Dispose()
        }
    }
} finally { $env:ASNAPDO_DATA_DIR = $previousFolder }
