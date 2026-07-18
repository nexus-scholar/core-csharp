$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/resolve-dotnet.ps1"
$dotnet = Use-PinnedDotNet $root
Push-Location $root
try {
    & $dotnet restore NexusScholar.Core.slnx
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $dotnet build NexusScholar.Core.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $dotnet test NexusScholar.Core.slnx --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Nexus Scholar C# starter is ready.' -ForegroundColor Green
}
finally {
    Pop-Location
}
