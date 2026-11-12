# Downloads the single-file Windows build of Worksheet.net into the package's tools folder.
# Chocolatey shims that executable afterwards, which is what puts `worksheet` on the PATH; the
# worksheet.exe.gui beside it says the shim is for a window and must not wait for it.
#
# The URL and the checksum are substituted by scripts/generate-chocolatey-package.sh — read them
# from the .nuspec of a built package, never edit them here.
$ErrorActionPreference = 'Stop'
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url      = '__URL__'
$checksum = '__SHA256__'

Get-ChocolateyWebFile -PackageName 'worksheet.net' `
  -FileFullPath "$toolsDir\worksheet.exe" `
  -Url "$url" `
  -Checksum "$checksum" -ChecksumType 'sha256'
