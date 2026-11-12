# Installs Worksheet.net: one self-contained binary, no runtime to install.
#
#   irm https://raw.githubusercontent.com/d-led/worksheet.net/main/install.ps1 | iex
#
# What it installs is the newest build of `main` — there is no release to wait for.
#
# WORKSHEET_BIN_DIR  where to put the binary (default: %LOCALAPPDATA%\Programs\Worksheet)
# WORKSHEET_REPO     owner/repo to download from (default: d-led/worksheet.net)
# WORKSHEET_BRANCH   which branch's build to take (default: main)
# WORKSHEET_WORKFLOW the workflow that builds it (default: build)
# WORKSHEET_BASE_URL a directory holding worksheet-win-x64.exe itself, which skips the unzip

$ErrorActionPreference = 'Stop'

$repo = if ($env:WORKSHEET_REPO) { $env:WORKSHEET_REPO } else { 'd-led/worksheet.net' }
$branch = if ($env:WORKSHEET_BRANCH) { $env:WORKSHEET_BRANCH } else { 'main' }
$workflow = if ($env:WORKSHEET_WORKFLOW) { $env:WORKSHEET_WORKFLOW } else { 'build' }
$dir = if ($env:WORKSHEET_BIN_DIR) { $env:WORKSHEET_BIN_DIR } else { Join-Path $env:LOCALAPPDATA 'Programs\Worksheet' }

$asset = 'worksheet-win-x64.exe'
$target = Join-Path $dir 'worksheet.exe'

New-Item -ItemType Directory -Force -Path $dir | Out-Null

if ($env:WORKSHEET_BASE_URL) {
    Write-Host "Downloading $asset from $env:WORKSHEET_BASE_URL ..."
    Invoke-WebRequest -Uri "$env:WORKSHEET_BASE_URL/$asset" -OutFile $target
}
else {
    $latest = "https://nightly.link/$repo/workflows/$workflow/$branch/worksheet-win-x64.zip"
    $zip = Join-Path ([System.IO.Path]::GetTempPath()) "worksheet-win-x64.zip"

    Write-Host "Downloading the newest $branch build: $latest"
    Invoke-WebRequest -Uri $latest -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $dir -Force
    Remove-Item $zip -Force

    if (-not (Test-Path $target)) {
        Throw "The download did not contain $asset. Nothing was installed."
    }
}

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($userPath -notlike "*$dir*") {
    [Environment]::SetEnvironmentVariable('Path', "$userPath;$dir", 'User')
    Write-Host "Added $dir to your PATH; open a new terminal to use it."
}

Write-Host "Installed $target"
