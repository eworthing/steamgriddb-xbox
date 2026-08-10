# Builds, signs, and installs the dev fork package (SteamGridDB (fork)).
# The signing cert (CN=3A3A4DF3-61EC-44B3-8236-B38DEB2BFA98) must exist in Cert:\CurrentUser\My
# and its public key must be trusted in Cert:\LocalMachine\TrustedPeople.
# Game Bar only lists fully installed packages, so a loose Add-AppxPackage -Register deploy
# will build and run but never appear in the widget menu - always deploy via this script.
param(
    [string]$Platform = "x64",
    [string]$Configuration = "Debug",
    [string]$CertThumbprint = "196354983C0E5E43346AB1739021DC0DB68BC65F",
    [string]$PackageName = "eworthing.SteamGridDBforXbox.Dev"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found - is Visual Studio with the MSBuild component installed?" }

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.*\x64\signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $signtool) { throw "signtool.exe not found - install the Windows SDK signing tools" }

# Windows only treats an install as an update - and so only keeps LocalState - when the version goes
# up. Rebuilding the same version is refused as a reinstall (0x80073CFB), which forced an
# uninstall and wiped applied-artwork.json and last-fix.log. So the build field is bumped for the
# packaged copy only, and the manifest is put back afterwards to keep the working tree clean.
# Backed up as bytes, not text: the manifest is UTF-8 with a BOM and PowerShell would drop it.
$manifestPath = "$root\SteamGridDB.Xbox\Package.appxmanifest"
$manifestBackup = [System.IO.File]::ReadAllBytes($manifestPath)

try {
    $manifest = [xml](Get-Content $manifestPath -Raw)
    $version = [Version]$manifest.Package.Identity.Version

    # Relative to whatever is installed, not to the source, so repeated deploys keep climbing
    $installed = Get-AppxPackage -Name $PackageName | Select-Object -First 1
    if ($installed -and [Version]$installed.Version -ge $version) { $version = [Version]$installed.Version }

    $manifest.Package.Identity.Version = "{0}.{1}.{2}.0" -f $version.Major, $version.Minor, ($version.Build + 1)
    $manifest.Save($manifestPath)
    Write-Host "Packaging as $($manifest.Package.Identity.Version) so the install is an update, not a reinstall" -ForegroundColor DarkGray

    & $msbuild "$root\SteamGridDB.Xbox.sln" /p:Configuration=$Configuration /p:Platform=$Platform /p:AppxBundle=Never /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}
finally {
    [System.IO.File]::WriteAllBytes($manifestPath, $manifestBackup)
}

$msix = Get-ChildItem "$root\SteamGridDB.Xbox\AppPackages\*_${Platform}_${Configuration}_Test\*_${Platform}_${Configuration}.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
& $signtool sign /fd SHA256 /sha1 $CertThumbprint $msix
if ($LASTEXITCODE -ne 0) { throw "Signing failed" }

# Update in place rather than uninstall and reinstall, so LocalState survives - that is where
# applied-artwork.json and last-fix.log live, and wiping them on every deploy destroys exactly the
# state a redeploy-then-rerun cycle exists to compare against. The version never changes during
# development, so the update needs -ForceUpdateFromAnyVersion to be allowed at all.
# (-PreserveApplicationData is not an option here: it only applies to development-mode registrations,
# and Game Bar only lists fully installed packages.)
$updated = $false
if (Get-AppxPackage -Name $PackageName) {
    try {
        Add-AppxPackage -Path $msix -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop
        $updated = $true
    } catch {
        Write-Host "In-place update failed ($($_.Exception.Message.Split([Environment]::NewLine)[0])) - reinstalling, local state will be lost" -ForegroundColor Yellow
    }
}

if (-not $updated) {
    foreach ($existing in @(Get-AppxPackage -Name $PackageName)) {
        Remove-AppxPackage $existing.PackageFullName
    }
    Add-AppxPackage -Path $msix
}

Get-Process -Name GameBar, GameBarFTServer, XboxGameBarWidgets -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "Deployed $(Split-Path $msix -Leaf) - Game Bar restarted, widget list will refresh on next Win+G" -ForegroundColor Green
