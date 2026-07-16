$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Resolve-DotNet([string]$repositoryRoot) {
    $requiredSdk = (Get-Content -Raw (Join-Path $repositoryRoot 'global.json') | ConvertFrom-Json).sdk.version
    $executable = if ($env:OS -eq 'Windows_NT') { 'dotnet.exe' } else { 'dotnet' }
    $candidates = New-Object System.Collections.Generic.List[string]
    if ($env:DOTNET_ROOT) { $candidates.Add((Join-Path $env:DOTNET_ROOT $executable)) }
    $homeDirectory = if ($env:USERPROFILE) { $env:USERPROFILE } else { $env:HOME }
    if ($homeDirectory) { $candidates.Add((Join-Path (Join-Path $homeDirectory '.dotnet') $executable)) }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $candidates.Add($command.Source) }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $candidate)) { continue }
        $installed = & $candidate --list-sdks 2>$null
        if ($LASTEXITCODE -eq 0 -and $installed -match ('^' + [Regex]::Escape($requiredSdk) + ' \[')) {
            return $candidate
        }
    }
    throw "Unable to locate a dotnet host containing the pinned SDK $requiredSdk."
}

function Get-Sha256Hex([byte[]]$bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '')
    }
    finally { $sha.Dispose() }
}

$dotnet = Resolve-DotNet $root
Push-Location $root
try {
    & (Join-Path $PSScriptRoot 'verify-release-policy.ps1')
    & (Join-Path $PSScriptRoot 'verify-release-policy-regressions.ps1')

    $topology = Get-Content -Raw eng/package-topology.json | ConvertFrom-Json
    $version = $topology.version
    $packageDirectory = Join-Path $root 'artifacts/packages'
    $repeatDirectory = Join-Path $root 'artifacts/packages-repeat'
    $smokePackages = Join-Path $root 'tests/NexusScholar.PackageSmoke/.packages'
    $smokeBin = Join-Path $root 'tests/NexusScholar.PackageSmoke/bin'
    $smokeObj = Join-Path $root 'tests/NexusScholar.PackageSmoke/obj'

    Remove-Item $packageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $repeatDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $smokePackages -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $smokeBin -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $smokeObj -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $packageDirectory -ItemType Directory -Force | Out-Null
    New-Item $repeatDirectory -ItemType Directory -Force | Out-Null

    & $dotnet pack NexusScholar.Core.slnx -c Release --no-build --no-restore -p:PackageVersion=$version -o $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw "Package creation failed with exit code $LASTEXITCODE." }

    & $dotnet pack NexusScholar.Core.slnx -c Release --no-build --no-restore -p:PackageVersion=$version -o $repeatDirectory
    if ($LASTEXITCODE -ne 0) { throw "Repeat package creation failed with exit code $LASTEXITCODE." }

    $packages = Get-ChildItem $packageDirectory -Filter *.nupkg | Where-Object { $_.Name -notlike '*.symbols.nupkg' }
    $actualIds = @($packages | ForEach-Object { $_.BaseName.Substring(0, $_.BaseName.Length - ('.' + $version).Length) } | Sort-Object)
    $expectedIds = @($topology.packages | Sort-Object)
    if (Compare-Object $expectedIds $actualIds) {
        throw 'Packed artifacts do not match the approved package topology.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    function Get-NormalizedPackageDigest([string]$path) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
        try {
            $lines = foreach ($entry in $archive.Entries | Sort-Object FullName) {
                if ($entry.FullName -eq '[Content_Types].xml' -or
                    $entry.FullName.StartsWith('_rels/', [StringComparison]::Ordinal) -or
                    $entry.FullName.StartsWith('package/services/metadata/core-properties/', [StringComparison]::Ordinal)) {
                    continue
                }
                $stream = $entry.Open()
                try {
                    $memory = [System.IO.MemoryStream]::new()
                    try {
                        $stream.CopyTo($memory)
                        $hash = Get-Sha256Hex $memory.ToArray()
                    }
                    finally { $memory.Dispose() }
                }
                finally { $stream.Dispose() }
                "$($entry.FullName)=$hash"
            }
            $material = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
            return Get-Sha256Hex $material
        }
        finally { $archive.Dispose() }
    }

    foreach ($package in $packages) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $entryNames = @($archive.Entries.FullName)
            if ('README.md' -notin $entryNames -or 'LICENSE' -notin $entryNames) {
                throw "Package '$($package.Name)' is missing README.md or LICENSE."
            }
            $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
            if ($nuspecEntries.Count -ne 1) {
                throw "Package '$($package.Name)' must contain exactly one nuspec."
            }
            $nuspecEntry = $nuspecEntries[0]
            $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
            try { $nuspec = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($nuspec.package.metadata.version -ne $version -or $nuspec.package.metadata.license.InnerText -ne 'MIT') {
                throw "Package '$($package.Name)' has invalid version or license metadata."
            }
        }
        finally {
            $archive.Dispose()
        }

        $repeatPackage = Join-Path $repeatDirectory $package.Name
        if (-not (Test-Path $repeatPackage) -or
            (Get-NormalizedPackageDigest $package.FullName) -ne (Get-NormalizedPackageDigest $repeatPackage)) {
            throw "Package '$($package.Name)' did not reproduce identical normalized content."
        }
    }

    & $dotnet restore tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj --source $packageDirectory --force-evaluate
    if ($LASTEXITCODE -ne 0) { throw "Package smoke restore failed with exit code $LASTEXITCODE." }
    & $dotnet run --project tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Package smoke execution failed with exit code $LASTEXITCODE." }

    $manifest = $packages | Sort-Object Name | ForEach-Object {
        [ordered]@{
            file = $_.Name
            sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            normalizedContentSha256 = Get-NormalizedPackageDigest $_.FullName
        }
    }
    $manifestJson = [ordered]@{ schema = 'nexus.package-manifest.v1'; version = $version; packages = @($manifest) } |
        ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText(
        (Join-Path $packageDirectory 'package-manifest.json'),
        $manifestJson,
        (New-Object Text.UTF8Encoding($false)))

    Write-Host "Package verification passed: $($packages.Count) packages at version $version; clean local-source smoke succeeded."
}
finally {
    Pop-Location
}
