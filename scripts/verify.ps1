$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    & "$PSScriptRoot/verify-release-policy.ps1"

    dotnet restore NexusScholar.Core.slnx
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build NexusScholar.Core.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & "$PSScriptRoot/verify-packages.ps1"

    & "$PSScriptRoot/build-release-evidence.ps1"

    dotnet test NexusScholar.Core.slnx --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- sample
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- demo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
