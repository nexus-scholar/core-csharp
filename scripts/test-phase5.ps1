$ErrorActionPreference = "Stop"

dotnet build NexusScholar.Core.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
dotnet test NexusScholar.Core.slnx -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
dotnet test NexusScholar.Core.slnx -c Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/coverage
if ($LASTEXITCODE -ne 0) { throw "Coverage run failed with exit code $LASTEXITCODE." }
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) { throw "Format verification failed with exit code $LASTEXITCODE." }

Write-Host "Coverage reports are informational. Scientific invariant tests remain the gate."
