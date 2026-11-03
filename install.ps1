# Installs Worksheet: one self-contained binary, no runtime to install.
#
#   irm https://raw.githubusercontent.com/d-led/worksheet.net/main/install.ps1 | iex
#
# WORKSHEET_BIN_DIR  where to put the binary (default: %LOCALAPPDATA%\Programs\Worksheet)
# WORKSHEET_REPO     owner/repo to download from
# WORKSHEET_BASE_URL download root, instead of the latest GitHub release

$ErrorActionPreference = 'Stop'

$repo = if ($env:WORKSHEET_REPO) { $env:WORKSHEET_REPO } else { 'd-led/worksheet.net' }
$base = if ($env:WORKSHEET_BASE_URL) { $env:WORKSHEET_BASE_URL } else { "https://github.com/$repo/releases/latest/download" }
$dir = if ($env:WORKSHEET_BIN_DIR) { $env:WORKSHEET_BIN_DIR } else { Join-Path $env:LOCALAPPDATA 'Programs\Worksheet' }

$asset = 'worksheet-win-x64.exe'
$target = Join-Path $dir 'worksheet.exe'

Write-Host "Downloading $asset ..."
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Invoke-WebRequest -Uri "$base/$asset" -OutFile $target

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($userPath -notlike "*$dir*") {
    [Environment]::SetEnvironmentVariable('Path', "$userPath;$dir", 'User')
    Write-Host "Added $dir to your PATH; open a new terminal to use it."
}

Write-Host "Installed $target"
