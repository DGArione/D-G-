<#
.SYNOPSIS
  Publishes D!G! as a self-contained, single-file win-x64 executable.
.PARAMETER Configuration
  Build configuration (default Release).
.PARAMETER Version
  SemVer version stamped into the assembly (default 0.1.0).
.PARAMETER Output
  Output directory for the published app (default artifacts/app).
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$Output = "artifacts/app"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Write-Host "Publishing D!G! $Version ($Configuration) -> $Output" -ForegroundColor Cyan
    dotnet publish "src/DIG.App/DIG.App.csproj" `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$Version `
        -p:DebugType=none `
        -o $Output
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    Write-Host "Published files:" -ForegroundColor Green
    Get-ChildItem $Output | Format-Table Name, Length
}
finally {
    Pop-Location
}
