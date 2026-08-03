# Builds, signs, and installs the dev fork package (SteamGridDB (fork)).
# The signing cert (CN=3A3A4DF3-61EC-44B3-8236-B38DEB2BFA98) must exist in Cert:\CurrentUser\My
# and its public key must be trusted in Cert:\LocalMachine\TrustedPeople.
# Game Bar only lists fully installed packages, so a loose Add-AppxPackage -Register deploy
# will build and run but never appear in the widget menu - always deploy via this script.
param(
    [string]$Platform = "x64",
    [string]$Configuration = "Debug",
    [string]$CertThumbprint = "196354983C0E5E43346AB1739021DC0DB68BC65F"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.*\x64\signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName

& $msbuild "$root\SteamGridDB.Xbox.sln" /p:Configuration=$Configuration /p:Platform=$Platform /p:AppxBundle=Never /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$msix = Get-ChildItem "$root\SteamGridDB.Xbox\AppPackages\*_${Platform}_${Configuration}_Test\*_${Platform}_${Configuration}.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
& $signtool sign /fd SHA256 /sha1 $CertThumbprint $msix
if ($LASTEXITCODE -ne 0) { throw "Signing failed" }

$existing = Get-AppxPackage -Name eworthing.SteamGridDBforXbox.Dev
if ($existing) { Remove-AppxPackage $existing.PackageFullName }
Add-AppxPackage -Path $msix

Get-Process -Name GameBar, GameBarFTServer, XboxGameBarWidgets -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "Deployed $(Split-Path $msix -Leaf) - Game Bar restarted, widget list will refresh on next Win+G" -ForegroundColor Green
