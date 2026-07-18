#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$root/scripts/resolve-dotnet.sh"
dotnet_host="$(resolve_pinned_dotnet "$root")"
cd "$root"

"$dotnet_host" restore NexusScholar.Core.slnx
"$dotnet_host" build NexusScholar.Core.slnx --configuration Release --no-restore
"$dotnet_host" test NexusScholar.Core.slnx --configuration Release --no-build
"$dotnet_host" run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor

echo "Nexus Scholar C# starter is ready."
