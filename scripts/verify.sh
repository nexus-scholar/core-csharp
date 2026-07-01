#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx --configuration Release --no-restore
dotnet test NexusScholar.Core.slnx --configuration Release --no-build
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- sample
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- demo
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
