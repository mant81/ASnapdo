param([string]$Repository = 'mant81/ASnapdo')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    [xml]$project = Get-Content 'src/ASnapdo/ASnapdo.csproj' -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
    $tag = "v$version"
    $installer = Join-Path $PSScriptRoot "release/setup/asnapdo_inst_x64_$version.exe"
    if (-not (Test-Path -LiteralPath $installer)) { throw 'Build the installer first with build-installer.ps1.' }
    if ((Get-Item -LiteralPath $installer).VersionInfo.ProductVersion -ne $version) { throw 'Installer version does not match project.' }
    $dirty = git status --porcelain
    if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Commit and push all source changes before publishing.' }
    $commit = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read release commit.' }
    $remote = git ls-remote origin refs/heads/main
    if ($LASTEXITCODE -ne 0 -or ($remote -split '\s+')[0] -ne $commit) { throw 'Push the release commit to origin/main first.' }
    $token = $env:GH_TOKEN
    if (-not $token) { $token = $env:GITHUB_TOKEN }
    if (-not $token) {
        $credential = "protocol=https`nhost=github.com`n`n" | git credential fill
        if ($LASTEXITCODE -ne 0) { throw 'GitHub authentication is required.' }
        $token = ($credential | Where-Object { $_.StartsWith('password=') } | Select-Object -First 1) -replace '^password=', ''
        $credential = $null
    }
    if (-not $token) { throw 'No GitHub access token is available.' }
    $headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json'; 'User-Agent' = 'ASnapdo-Release' }
    $api = "https://api.github.com/repos/$Repository"
    $generated = Invoke-RestMethod -Uri "$api/releases/generate-notes" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{ tag_name = $tag; target_commitish = $commit } | ConvertTo-Json)
    $changelog = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CHANGELOG.md'))
    $section = [regex]::Match($changelog, '(?ms)^## ' + [regex]::Escape($version) + ' .+?(?=^## |\z)').Value.Trim()
    if (-not $section) { throw 'Current release is missing from CHANGELOG.md.' }
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    $body = $section + "`n`nSHA-256: ``$hash```n`n" + $generated.body
    $release = Invoke-RestMethod -Uri "$api/releases" -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes((@{ tag_name = $tag; target_commitish = $commit; name = "ASnapdo $version"; body = $body; draft = $true } | ConvertTo-Json)))
    $upload = ($release.upload_url -replace '\{.*$', '') + '?name=' + [Uri]::EscapeDataString([IO.Path]::GetFileName($installer))
    $asset = Invoke-RestMethod -Uri $upload -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $installer
    if ($asset.size -ne (Get-Item -LiteralPath $installer).Length -or $asset.state -ne 'uploaded') { throw 'Installer upload verification failed; release remains a draft.' }
    $published = Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Method Patch -Headers $headers -ContentType 'application/json' -Body '{"draft":false}'
    Write-Output "Release: $($published.html_url)"
    Write-Output "Installer: $($asset.browser_download_url)"
    Write-Output "SHA-256: $hash"
} finally {
    $token = $null; $headers = $null
    Pop-Location
}
