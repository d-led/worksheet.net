# Unpacks the Windows folder build of Worksheet.net into the package's tools folder.
# The folder ships as a zip holding worksheet.exe beside the dll the apphost finds for itself. Both
# land next to the empty worksheet.exe.gui this package carries, and that marker — same name as the
# executable, in the folder Chocolatey shims from — is what says the shim is for a window and must
# not wait for it. Nothing here makes the shim: Chocolatey shims every executable it finds in the
# package folder once this script has run, and takes the shim and the files away on uninstall.
#
# The URL and the checksum are substituted by scripts/generate-chocolatey-package.sh — read them
# from the .nuspec of a built package, never edit them here.
$ErrorActionPreference = 'Stop'
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url      = '__URL__'
$checksum = '__SHA256__'
$zip      = Join-Path $env:TEMP 'worksheet.net.zip'

Install-ChocolateyZipPackage -PackageName 'worksheet.net' `
  -Url "$url" `
  -UnzipLocation "$toolsDir" `
  -FileFullPath "$zip" `
  -Checksum "$checksum" -ChecksumType 'sha256'

# The archive has done its work; what stays in tools is the application itself.
Remove-Item "$zip" -Force -ErrorAction SilentlyContinue
