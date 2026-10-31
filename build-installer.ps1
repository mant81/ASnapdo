param(
    [string]$Compiler = "$PSScriptRoot\tools\vendor\InnoSetup\ISCC.exe",
    [string]$CertificateThumbprint = $env:ASNAPDO_SIGNING_THUMBPRINT,
    [string]$SignTool,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$MachineCertificateStore
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Inno Setup ISCC.exe is required. Pass -Compiler with its path.' }
    if ($CertificateThumbprint) {
        $CertificateThumbprint = $CertificateThumbprint -replace '\s', ''
        if ($CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$') { throw 'CertificateThumbprint must be a SHA-1 certificate thumbprint (40 hex characters).' }
        $store = if ($MachineCertificateStore) { 'LocalMachine' } else { 'CurrentUser' }
        $certificate = Get-Item -LiteralPath "Cert:\$store\My\$CertificateThumbprint"
        if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
            throw 'Signing requires a currently valid certificate with an accessible private key.'
        }
        if (-not ($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId.Value -eq '1.3.6.1.5.5.7.3.3' })) {
            throw 'The certificate must have the Code Signing enhanced key usage.'
        }
        if (-not $SignTool) {
            $SignTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" |
                Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
                Select-Object -First 1 -ExpandProperty FullName
        }
        if (-not $SignTool -or -not (Test-Path -LiteralPath $SignTool)) { throw 'Install Windows SDK SignTool or pass -SignTool.' }
        function Sign-ReleaseFile([string]$Path) {
            $signArguments = @('sign', '/sha1', $CertificateThumbprint, '/s', 'My', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')
            if ($MachineCertificateStore) { $signArguments += '/sm' }
            & $SignTool @signArguments $Path
            if ($LASTEXITCODE -ne 0) { throw "Code signing failed: $Path" }
            & $SignTool verify /pa /all /v $Path
            if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $Path" }
            $signature = Get-AuthenticodeSignature -LiteralPath $Path
            if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate) {
                throw "A valid timestamped signature is required: $Path"
            }
        }
    } else {
        Write-Warning 'Unsigned development build: public downloads may display a Microsoft Defender SmartScreen warning. Use -CertificateThumbprint for a signed release.'
    }
    & "$PSScriptRoot\build-exe.cmd"
    if ($LASTEXITCODE -ne 0) { throw 'Editor publish failed.' }
    if ($CertificateThumbprint) {
        foreach ($file in @('release\app\ASnapdo.exe', 'release\app\editor\ASnapdo.exe', 'release\app\editor\ASnapdo.dll')) {
            Sign-ReleaseFile (Join-Path $PSScriptRoot $file)
        }
    }
    & $Compiler /Q installer/ASnapdo.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    if ($CertificateThumbprint) {
        Sign-ReleaseFile (Join-Path $PSScriptRoot 'release\setup\asnapdo_inst_x64_1.0.2.exe')
    }
} finally { Pop-Location }
