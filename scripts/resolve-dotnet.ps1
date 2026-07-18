function Resolve-PinnedDotNet([string]$RepositoryRoot) {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Split-Path -Parent $PSScriptRoot
    }

    $globalJsonPath = Join-Path $RepositoryRoot 'global.json'
    if (-not (Test-Path -LiteralPath $globalJsonPath)) {
        throw "Unable to find global.json under '$RepositoryRoot'."
    }

    $requiredSdk = (Get-Content -Raw -LiteralPath $globalJsonPath | ConvertFrom-Json).sdk.version
    $executable = if ($env:OS -eq 'Windows_NT') { 'dotnet.exe' } else { 'dotnet' }
    $candidates = [System.Collections.Generic.List[string]]::new()

    if ($env:DOTNET_ROOT) {
        $candidates.Add((Join-Path $env:DOTNET_ROOT $executable))
    }

    $homeDirectory = if ($env:USERPROFILE) { $env:USERPROFILE } else { $env:HOME }
    if ($homeDirectory) {
        $candidates.Add((Join-Path (Join-Path $homeDirectory '.dotnet') $executable))
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        $candidates.Add($command.Source)
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $candidate)) {
            continue
        }

        $installed = & $candidate --list-sdks 2>$null
        if ($LASTEXITCODE -eq 0 -and
            $installed -match ('(?m)^' + [Regex]::Escape($requiredSdk) + ' \[')) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Unable to locate the SDK pinned by global.json ($requiredSdk). Install that exact SDK or set DOTNET_ROOT to a host that contains it."
}

function Use-PinnedDotNet([string]$RepositoryRoot) {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Split-Path -Parent $PSScriptRoot
    }

    $hostPath = Resolve-PinnedDotNet $RepositoryRoot
    $hostDirectory = Split-Path -Parent $hostPath
    $separator = [IO.Path]::PathSeparator
    $pathEntries = @($env:PATH -split [Regex]::Escape([string]$separator))
    if ($pathEntries -notcontains $hostDirectory) {
        $env:PATH = "$hostDirectory$separator$env:PATH"
    }

    return $hostPath
}
