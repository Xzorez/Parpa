<#
.SYNOPSIS
    Publica Parpa y crea el instalador (artifacts\Parpa-Setup-<versión>-<arch>.exe).

.EXAMPLE
    .\tools\build-installer.ps1 -Version 1.0.0
    .\tools\build-installer.ps1 -Version 1.0.0 -Arch arm64 -Step publish   # solo "dotnet publish" (para firmar antes de empaquetar)
    .\tools\build-installer.ps1 -Version 1.0.0 -Step installer             # solo Inno Setup sobre lo ya publicado
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [ValidateSet('x64', 'arm64')] [string] $Arch = 'x64',
    [ValidateSet('all', 'publish', 'installer')] [string] $Step = 'all',
    [string] $Repo = $env:GITHUB_REPOSITORY
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts "publish-$Arch"

if ($Step -in 'all', 'publish') {
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    $platform = if ($Arch -eq 'arm64') { 'ARM64' } else { 'x64' }
    dotnet publish (Join-Path $root 'Parpa.App\Parpa.App.csproj') -c Release -r "win-$Arch" -p:Platform=$platform `
        --self-contained -p:Version=$Version -o $publishDir -nologo
    if ($LASTEXITCODE) { throw "dotnet publish falló ($LASTEXITCODE)" }
}

if ($Step -in 'all', 'installer') {
    $iscc = @(
        (Get-Command iscc -ErrorAction SilentlyContinue).Source,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'No encuentro Inno Setup 6 (ISCC.exe). Instálalo con: winget install JRSoftware.InnoSetup' }

    $defines = @("/DAppVersion=$Version", "/DArch=$Arch", "/DSourceDir=$publishDir", "/DOutputDir=$artifacts")
    if ($Repo) { $defines += "/DRepo=$Repo" }
    & $iscc /Q @defines (Join-Path $root 'installer\Parpa.iss')
    if ($LASTEXITCODE) { throw "Inno Setup falló ($LASTEXITCODE)" }
    Get-Item (Join-Path $artifacts "Parpa-Setup-$Version-$Arch.exe")
}
