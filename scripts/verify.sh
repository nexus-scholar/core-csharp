#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$root/scripts/resolve-dotnet.sh"
dotnet_host="$(resolve_pinned_dotnet "$root")"
cd "$root"

"$dotnet_host" restore NexusScholar.Core.slnx
"$dotnet_host" build NexusScholar.Core.slnx --configuration Release --no-restore
"$dotnet_host" test NexusScholar.Core.slnx --configuration Release --no-build
if ! command -v pwsh >/dev/null 2>&1; then
  echo "PowerShell 7 (pwsh) is required for phase-5 mutation verification." >&2
  exit 1
fi

pwsh ./scripts/mutation-phase5.ps1
"$dotnet_host" run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor
"$dotnet_host" run --project src/NexusScholar.Cli --configuration Release --no-build -- sample
"$dotnet_host" run --project src/NexusScholar.Cli --configuration Release --no-build -- demo
"$dotnet_host" format NexusScholar.Core.slnx --verify-no-changes --no-restore
