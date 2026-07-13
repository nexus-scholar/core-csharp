$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $topology = Get-Content -Raw eng/package-topology.json | ConvertFrom-Json
    $version = $topology.version
    $packageDirectory = Join-Path $root 'artifacts/packages'
    $repeatDirectory = Join-Path $root 'artifacts/packages-repeat'
    $smokePackages = Join-Path $root 'tests/NexusScholar.PackageSmoke/.packages'

    Remove-Item $packageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $repeatDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $smokePackages -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $packageDirectory -ItemType Directory -Force | Out-Null
    New-Item $repeatDirectory -ItemType Directory -Force | Out-Null

    dotnet pack NexusScholar.Core.slnx -c Release --no-build --no-restore -p:PackageVersion=$version -o $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw "Package creation failed with exit code $LASTEXITCODE." }

    dotnet pack NexusScholar.Core.slnx -c Release --no-build --no-restore -p:PackageVersion=$version -o $repeatDirectory
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
                        $hash = [System.Security.Cryptography.SHA256]::HashData($memory.ToArray())
                    }
                    finally { $memory.Dispose() }
                }
                finally { $stream.Dispose() }
                "$($entry.FullName)=$([Convert]::ToHexString($hash).ToLowerInvariant())"
            }
            $material = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
            return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($material)).ToLowerInvariant()
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

    dotnet restore tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj --source $packageDirectory --force-evaluate
    if ($LASTEXITCODE -ne 0) { throw "Package smoke restore failed with exit code $LASTEXITCODE." }
    dotnet run --project tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Package smoke execution failed with exit code $LASTEXITCODE." }

    $manifest = $packages | Sort-Object Name | ForEach-Object {
        [ordered]@{
            file = $_.Name
            sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            normalizedContentSha256 = Get-NormalizedPackageDigest $_.FullName
        }
    }
    [ordered]@{ schema = 'nexus.package-manifest.v1'; version = $version; packages = @($manifest) } |
        ConvertTo-Json -Depth 4 | Set-Content (Join-Path $packageDirectory 'package-manifest.json') -Encoding utf8NoBOM

    Write-Host "Package verification passed: $($packages.Count) packages at version $version; clean local-source smoke succeeded."
}
finally {
    Pop-Location
}
