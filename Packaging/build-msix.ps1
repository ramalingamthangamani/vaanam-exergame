# Packages Builds/Windows/ (made by Vaanam > Build Windows Player) into an MSIX
# for Microsoft Store submission.
#
# Prerequisites
#   - Windows 10 SDK (makeappx.exe, signtool.exe). Found automatically.
#   - A Partner Center app reservation. Copy these from Partner Center >
#     Product identity: Package/Identity/Name, Package/Identity/Publisher,
#     Package/Properties/PublisherDisplayName.
#   - Store icons in Packaging/Assets (StoreLogo 50x50, Square150x150Logo,
#     Square44x44Logo, Wide310x150Logo, SplashScreen 620x300). Placeholders are
#     generated if missing so the packaging step never blocks on art.
#
# Usage
#   .\Packaging\build-msix.ps1 -IdentityName "12345Publisher.Vaanam" `
#       -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplay "Your Name" -Version 1.0.0.0
#
# For LOCAL testing only, add -SelfSign to create a test certificate, sign the
# package and install it. Store uploads must NOT be self-signed; the Store signs
# them for you.
param(
    [Parameter(Mandatory = $true)][string]$IdentityName,
    [Parameter(Mandatory = $true)][string]$Publisher,
    [Parameter(Mandatory = $true)][string]$PublisherDisplay,
    [string]$Version = "1.0.0.0",
    [switch]$SelfSign
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root "Builds\Windows"
$staging = Join-Path $root "Builds\MSIX\staging"
$outDir = Join-Path $root "Builds\MSIX"
if (-not (Test-Path (Join-Path $build "Vaanam.exe"))) { throw "Build the player first: Unity menu Vaanam > Build Windows Player (expects $build\Vaanam.exe)" }

$sdkBin = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Directory | Where-Object { $_.Name -match '^10\.' } | Sort-Object Name -Descending | Select-Object -First 1
$makeappx = Join-Path $sdkBin.FullName "x64\makeappx.exe"
$signtool = Join-Path $sdkBin.FullName "x64\signtool.exe"
if (-not (Test-Path $makeappx)) { throw "makeappx.exe not found under Windows Kits. Install the Windows 10/11 SDK." }

# ---- stage files -------------------------------------------------------------
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item (Join-Path $build "*") $staging -Recurse -Force
# Unity debug files are not needed in the Store package.
Get-ChildItem $staging -Recurse -Include *.pdb, *_BackUpThisFolder_ButDontShipItWithYourGame* | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# ---- icons (placeholders if missing) ------------------------------------------
$assetSrc = Join-Path $PSScriptRoot "Assets"
$assetDst = Join-Path $staging "Assets"
New-Item -ItemType Directory -Force $assetSrc, $assetDst | Out-Null
Add-Type -AssemblyName System.Drawing
function Placeholder([string]$name, [int]$w, [int]$h) {
    $path = Join-Path $assetSrc $name
    if (Test-Path $path) { return }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 58, 123, 213))
    $g.SmoothingMode = 'AntiAlias'
    $r = [math]::Min($w, $h) * 0.28
    $g.FillEllipse([System.Drawing.Brushes]::Orange, ($w / 2 - $r), ($h / 2 - $r), ($r * 2), ($r * 2))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Orange, ($r * 0.35)); $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLine($pen, ($w / 2 - $r * 0.6), ($h / 2), ($w / 2 - $r * 1.9), ($h / 2 - $r * 1.1))
    $g.DrawLine($pen, ($w / 2 + $r * 0.6), ($h / 2), ($w / 2 + $r * 1.9), ($h / 2 - $r * 1.1))
    $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "  placeholder icon: $name"
}
Placeholder "StoreLogo.png" 50 50
Placeholder "Square150x150Logo.png" 150 150
Placeholder "Square44x44Logo.png" 44 44
Placeholder "Wide310x150Logo.png" 310 150
Placeholder "SplashScreen.png" 620 300
Copy-Item (Join-Path $assetSrc "*.png") $assetDst -Force

# ---- manifest ------------------------------------------------------------------
$manifest = Get-Content (Join-Path $PSScriptRoot "AppxManifest.template.xml") -Raw
$manifest = $manifest.Replace("__IDENTITY_NAME__", $IdentityName).Replace("__PUBLISHER__", $Publisher).Replace("__PUBLISHER_DISPLAY__", $PublisherDisplay).Replace("__VERSION__", $Version)
Set-Content -Path (Join-Path $staging "AppxManifest.xml") -Value $manifest -Encoding utf8

# ---- pack ---------------------------------------------------------------------
New-Item -ItemType Directory -Force $outDir | Out-Null
$msix = Join-Path $outDir "Vaanam_$Version`_x64.msix"
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $staging /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }
Write-Host "MSIX written: $msix"

if ($SelfSign) {
    $pfx = Join-Path $outDir "VaanamTest.pfx"
    if (-not (Test-Path $pfx)) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName "Vaanam test" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        $pwd = ConvertTo-SecureString -String "vaanam" -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pwd | Out-Null
        Write-Host "Test certificate created: $pfx (password: vaanam). Import it into Trusted People to install the package."
    }
    & $signtool sign /fd SHA256 /a /f $pfx /p vaanam $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
    Write-Host "Signed for local testing. Install with: Add-AppxPackage `"$msix`""
}
else {
    Write-Host "Unsigned package (correct for Store upload: Partner Center signs it). Upload at https://partner.microsoft.com/dashboard"
}
