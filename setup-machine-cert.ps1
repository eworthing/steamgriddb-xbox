#Requires -RunAsAdministrator
<#
    Creates the dev code-signing cert that deploy-dev.ps1 signs the package with.
    Run once per machine, elevated.

    The key goes in Cert:\LocalMachine\My rather than Cert:\CurrentUser\My. A CurrentUser key is
    DPAPI-bound to the profile that created it, so a second Windows profile on the same PC cannot
    read it and cannot deploy at all - the .cer in the repo is only the public half and does not
    help, because signing needs the private key. The machine store guards the key with a file ACL
    instead, which can be granted to whichever accounts actually build.

    The subject must stay CN=3A3A4DF3-... : it has to match Package.appxmanifest's Publisher, and
    the PackageFamilyName is derived from that string. Regenerating with the same subject therefore
    keeps the package identity - and any installed package's upgrade path - intact.
#>
param(
    [string]$Subject = "CN=3A3A4DF3-61EC-44B3-8236-B38DEB2BFA98",

    # Who may sign without elevation. SYSTEM and Administrators keep access regardless.
    # Defaults to the built-in Users group, which every local account belongs to, so any profile
    # on the PC can deploy without this having to name them. Given by SID rather than name because
    # "BUILTIN\Users" is localised and does not resolve on a non-English Windows.
    # Accepts SIDs or account names: -SigningAccounts "$env:USERDOMAIN\alice" narrows it to one
    # profile. Note that whoever is listed can sign code this machine trusts.
    [string[]]$SigningAccounts = @("S-1-5-32-545")
)

$ErrorActionPreference = "Stop"

# --- 1. Mint the cert in the machine store -------------------------------------
# Extensions mirror the original cert: critical DigitalSignature, Code Signing EKU, critical
# end-entity Basic Constraints. CertEnroll spells that last one "ca=0" - the "Subject Type=End
# Entity" form shown in the Microsoft docs is the display text and is rejected on parse.
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA -KeyLength 2048 `
    -KeyExportPolicy Exportable `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -FriendlyName "SteamGridDB Xbox dev signing (machine-wide)" `
    -NotAfter (Get-Date).AddYears(3) `
    -TextExtension @(
        "2.5.29.37={text}1.3.6.1.5.5.7.3.3",
        "2.5.29.19={critical}{text}ca=0"
    )

Write-Host "Created $($cert.Thumbprint)  $($cert.Subject)" -ForegroundColor Green

# --- 2. Trust the public half machine-wide -------------------------------------
# Add-AppxPackage rejects the signature without this. TrustedPeople is machine-scoped, so one
# import covers every profile.
$cerPath = Join-Path $env:TEMP "sgdb-machine-pub.cer"
try {
    Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
    Write-Host "Trusted in LocalMachine\TrustedPeople" -ForegroundColor Green
}
finally {
    Remove-Item $cerPath -ErrorAction SilentlyContinue
}

# --- 3. Let the listed accounts read the private key ---------------------------
# New-SelfSignedCertificate produces a CNG key whose material is a file under ProgramData, ACLed to
# SYSTEM and Administrators only. Granting Read is what makes signing work from a normal shell.
$key     = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$keyFile = Join-Path "$env:ProgramData\Microsoft\Crypto\Keys" $key.Key.UniqueName
if (-not (Test-Path $keyFile)) { throw "Private key file not found at $keyFile" }

$acl = Get-Acl $keyFile
foreach ($account in $SigningAccounts) {
    # A SID string is turned into a SecurityIdentifier; anything else is treated as an account name.
    try   { $identity = [System.Security.Principal.SecurityIdentifier]$account }
    catch { $identity = [System.Security.Principal.NTAccount]$account }

    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($identity, "Read", "Allow")))

    $shown = $account
    try { $shown = $identity.Translate([System.Security.Principal.NTAccount]).Value } catch { }
    Write-Host "Granted Read to $shown" -ForegroundColor Green
}
Set-Acl -Path $keyFile -AclObject $acl

# --- 4. Report -----------------------------------------------------------------
# Each run mints a fresh keypair, so the thumbprint changes and deploy-dev.ps1's default needs
# updating to match. The old cert should be left in TrustedPeople if a package signed by it is
# still installed anywhere, or that install stops verifying.
Write-Host ""
Write-Host "THUMBPRINT: $($cert.Thumbprint)" -ForegroundColor Cyan
Write-Host "Set this as -CertThumbprint in deploy-dev.ps1." -ForegroundColor Cyan
