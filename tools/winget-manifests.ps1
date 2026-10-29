<#
.SYNOPSIS
    Genera los manifiestos de winget (Parpa.Parpa) para una versión publicada en GitHub Releases.
    Se suben a microsoft/winget-pkgs en manifests/p/Parpa/Parpa/<versión>/ (ver docs/RELEASING.md).

.EXAMPLE
    .\tools\winget-manifests.ps1 -Version 1.0.0 -Repo usuario/Parpa -Installers artifacts\Parpa-Setup-1.0.0-x64.exe, artifacts\Parpa-Setup-1.0.0-arm64.exe
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Repo,
    [Parameter(Mandatory)] [string[]] $Installers,
    [string] $OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\winget\$Version")
)

$ErrorActionPreference = 'Stop'
$id = 'Parpa.Parpa'
$url = "https://github.com/$Repo"
# Mismo AppId que installer\Parpa.iss: así winget reconoce la instalación para actualizar.
$productCode = '{69656D3B-8A28-462D-9D42-35AD2663F018}_is1'
$manifestVersion = '1.12.0'
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Write-Manifest([string] $name, [string] $text) {
    $path = Join-Path $OutDir $name
    [IO.File]::WriteAllText($path, $text.TrimStart() + "`n", [Text.UTF8Encoding]::new($false))
    Get-Item $path
}

$entries = foreach ($file in $Installers) {
    $item = Get-Item $file
    $arch = if ($item.Name -match '-arm64\.exe$') { 'arm64' } else { 'x64' }
    $sha = (Get-FileHash $item.FullName -Algorithm SHA256).Hash
@"
  - Architecture: $arch
    InstallerUrl: $url/releases/download/v$Version/$($item.Name)
    InstallerSha256: $sha
"@
}

Write-Manifest "$id.yaml" @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$manifestVersion.schema.json
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $manifestVersion
"@

Write-Manifest "$id.installer.yaml" @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$manifestVersion.schema.json
PackageIdentifier: $id
PackageVersion: $Version
InstallerType: inno
Scope: user
InstallModes:
  - interactive
  - silent
  - silentWithProgress
UpgradeBehavior: install
MinimumOSVersion: 10.0.19041.0
ProductCode: '$productCode'
ReleaseDate: $(Get-Date -Format 'yyyy-MM-dd')
Installers:
$($entries -join "`n")
ManifestType: installer
ManifestVersion: $manifestVersion
"@

Write-Manifest "$id.locale.en-US.yaml" @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$manifestVersion.schema.json
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: Parpa
PublisherUrl: $url
PublisherSupportUrl: $url/issues
PrivacyUrl: $url/blob/main/PRIVACY.md
PackageName: Parpa
PackageUrl: $url
License: MIT
LicenseUrl: $url/blob/main/LICENSE
ShortDescription: A notch for Windows that shows music, notifications, files and notes at the top of the screen.
Description: Parpa adds an island at the top of the screen that opens when you hover it. Music from any app, notifications with quick actions, volume and battery indicators, clipboard history, a file shelf, tasks and notes. Everything stays on your PC.
Moniker: parpa
Tags:
  - notch
  - dynamic-island
  - music
  - notifications
  - clipboard
  - productivity
ReleaseNotesUrl: $url/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: $manifestVersion
"@

Write-Manifest "$id.locale.es-ES.yaml" @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.locale.$manifestVersion.schema.json
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: es-ES
Publisher: Parpa
PackageName: Parpa
License: MIT
ShortDescription: Un notch para Windows con música, avisos, archivos y notas arriba de la pantalla.
Description: Parpa añade una isla arriba de la pantalla que se abre al pasar el ratón. Música de cualquier app, avisos con acciones rápidas, indicadores de volumen y batería, historial del portapapeles, bandeja de archivos, tareas y notas. Todo se queda en tu PC.
ReleaseNotesUrl: $url/releases/tag/v$Version
ManifestType: locale
ManifestVersion: $manifestVersion
"@
