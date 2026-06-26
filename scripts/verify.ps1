$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet restore NexusScholar.Core.slnx
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build NexusScholar.Core.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test NexusScholar.Core.slnx --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
