<#
.SYNOPSIS
  Restores, builds and tests the D!G! solution.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet restore DIG.sln
    if ($LASTEXITCODE -ne 0) { throw "restore failed" }

    dotnet build DIG.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "build failed" }

    dotnet test tests/DIG.Tests/DIG.Tests.csproj -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "tests failed" }

    Write-Host "Build + tests OK" -ForegroundColor Green
}
finally {
    Pop-Location
}
