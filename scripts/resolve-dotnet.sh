#!/usr/bin/env bash

resolve_pinned_dotnet() {
  local repository_root="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
  local required_sdk
  required_sdk="$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$repository_root/global.json" | head -n 1)"
  if [[ -z "$required_sdk" ]]; then
    echo "Unable to read the pinned SDK from $repository_root/global.json." >&2
    return 1
  fi

  local candidates=()
  if [[ -n "${DOTNET_ROOT:-}" ]]; then
    candidates+=("$DOTNET_ROOT/dotnet")
  fi

  local home_directory="${HOME:-${USERPROFILE:-}}"
  if [[ -z "${home_directory}" && -d /root ]]; then
    home_directory="/root"
  fi
  if [[ -n "${home_directory}" ]]; then
    candidates+=("$home_directory/.dotnet/dotnet")
  fi

  candidates+=("/usr/share/dotnet/dotnet")
  candidates+=("/opt/dotnet/dotnet")

  if command -v dotnet >/dev/null 2>&1; then
    candidates+=("$(command -v dotnet)")
  fi

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate" ]] &&
      "$candidate" --list-sdks 2>/dev/null | grep -Eq "^${required_sdk//./\\.} \\["; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  echo "Unable to locate the SDK pinned by global.json ($required_sdk). Install that exact SDK or set DOTNET_ROOT to a host that contains it." >&2
  return 1
}
