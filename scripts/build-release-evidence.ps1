param(
    [switch] $RequireCleanSourceTree
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/resolve-dotnet.ps1"
$dotnet = Use-PinnedDotNet $root
Push-Location $root
try {
    function Test-Net8Runtime([string]$dotnetHost) {
        try {
            return [bool]((& $dotnetHost --list-runtimes 2>$null |
                Select-String '^Microsoft\.NETCore\.App 8\.'))
        }
        catch {
            return $false
        }
    }

    function Get-NuGetGlobalPackagesPath([string]$dotnetHost) {
        if ($env:NUGET_PACKAGES -and (Test-Path -LiteralPath $env:NUGET_PACKAGES)) {
            return (Resolve-Path -LiteralPath $env:NUGET_PACKAGES).Path
        }

        $nugetLocals = & $dotnetHost nuget locals global-packages --list 2>$null
        foreach ($line in $nugetLocals) {
            if ($line -match 'global-packages:\s*(.+)') {
                $path = $Matches[1].Trim()
                if (Test-Path -LiteralPath $path) {
                    return (Resolve-Path -LiteralPath $path).Path
                }
            }
        }

        return $null
    }

    function Resolve-Net8RuntimeHost([string]$dotnetHost) {
        $isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Windows)
        $dotnetExecutable = if ($isWindowsHost) { 'dotnet.exe' } else { 'dotnet' }
        $candidates = [System.Collections.Generic.List[string]]::new()

        if ($env:DOTNET_ROOT) {
            $candidates.Add((Join-Path $env:DOTNET_ROOT $dotnetExecutable))
        }

        $homeDirectory = if ($env:HOME) {
            $env:HOME
        } else {
            $env:USERPROFILE
        }
        if ($homeDirectory) {
            $candidates.Add((Join-Path (Join-Path $homeDirectory '.dotnet') $dotnetExecutable))
        }

        $candidates.Add('/usr/share/dotnet/dotnet')
        $candidates.Add('/opt/dotnet/dotnet')

        $command = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($command) {
            $candidates.Add($command.Source)
        }

        foreach ($candidate in $candidates | Select-Object -Unique) {
            if ((Test-Path -LiteralPath $candidate -PathType Leaf) -and
                (Test-Net8Runtime $candidate)) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }

        return $null
    }

    $sourceStatus = git status --porcelain --untracked-files=all
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect the source tree before generating release evidence."
    }
    $dirty = -not [string]::IsNullOrWhiteSpace(($sourceStatus | Out-String))
    if ($RequireCleanSourceTree -and $dirty) {
        throw 'Release evidence requires a clean source tree when -RequireCleanSourceTree is set. Commit or stash local changes before generating release artifacts.'
    }

    $topology = Get-Content -Raw eng/package-topology.json | ConvertFrom-Json
    $version = $topology.version
    $packageDirectory = Join-Path $root 'artifacts/packages'
    $releaseDirectory = Join-Path $root 'artifacts/release'

    if (-not (Test-Path (Join-Path $packageDirectory 'package-manifest.json'))) {
        throw 'Package evidence is missing. Run scripts/verify-packages.ps1 first.'
    }

    Remove-Item $releaseDirectory -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $releaseDirectory -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $packageDirectory '*') $releaseDirectory -Recurse

    & $dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "Tool restore failed with exit code $LASTEXITCODE." }

    $toolVersion = (Get-Content -Raw dotnet-tools.json | ConvertFrom-Json).tools.'microsoft.sbom.dotnettool'.version
    $sbomHost = $dotnet
    $sbomPrefix = @('tool', 'run', 'sbom-tool', '--')
    if (-not (Test-Net8Runtime $dotnet)) {
        $dotnet8 = Resolve-Net8RuntimeHost $dotnet
        if (-not $dotnet8) {
            throw 'Microsoft.Sbom.DotNetTool requires a .NET 8 runtime. Install the pinned 8.0 runtime before generating evidence.'
        }

        $globalPackagesPath = Get-NuGetGlobalPackagesPath $dotnet
        if (-not $globalPackagesPath) {
            throw 'Unable to resolve the NuGet global-packages path for Microsoft.Sbom.DotNetTool.'
        }

        $sbomToolPath = Join-Path $globalPackagesPath "microsoft.sbom.dotnettool/$toolVersion/tools/net8.0/any/Microsoft.Sbom.DotNetTool.dll"
        if (-not (Test-Path -LiteralPath $sbomToolPath)) {
            throw "Unable to locate Microsoft.Sbom.DotNetTool assembly at '$sbomToolPath'. Run: dotnet tool restore."
        }

        $sbomHost = $dotnet8
        $sbomPrefix = @((Resolve-Path -LiteralPath $sbomToolPath).Path)
    }

    $commit = (git rev-parse HEAD).Trim()
    $commitTimestamp = [DateTimeOffset]::Parse(
        (git show -s --format=%cI HEAD).Trim(),
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal).UtcDateTime.ToString(
            'yyyy-MM-ddTHH:mm:ssZ',
            [Globalization.CultureInfo]::InvariantCulture)
    $namespacePart = "$commit/$version"
    & $sbomHost @sbomPrefix Generate `
            -b $releaseDirectory `
            -bc (Join-Path $root 'tests/NexusScholar.PackageSmoke') `
            -pn NexusScholar.Core `
            -pv $version `
            -ps 'Organization: Nexus Scholar' `
            -nsb 'https://github.com/nexus-scholar-org/core-csharp' `
            -nsu $namespacePart `
            -gt $commitTimestamp `
            -D true `
            -F false `
            -V Warning
    if ($LASTEXITCODE -ne 0) { throw "SBOM generation failed with exit code $LASTEXITCODE." }

    & $sbomHost @sbomPrefix Validate `
            -b $releaseDirectory `
            -o (Join-Path $releaseDirectory 'sbom-validation.json') `
            -n `
            -F false `
            -mi SPDX:2.2 `
            -V Warning
    if ($LASTEXITCODE -ne 0) { throw "SBOM validation failed with exit code $LASTEXITCODE." }

    $lockFiles = @(Get-ChildItem $root -Recurse -Filter packages.lock.json |
        Where-Object {
            $_.FullName -notmatch '[\\/]artifacts[\\/]' -and
            $_.FullName -notmatch '[\\/]tests[\\/]NexusScholar\.PackageSmoke[\\/]packages\.lock\.json$'
        } |
        Sort-Object FullName)

    [xml]$solution = Get-Content (Join-Path $root 'NexusScholar.Core.slnx') -Raw
    $expectedLockPaths = @($solution.SelectNodes('//Project') | ForEach-Object {
        $projectPath = Join-Path $root $_.Path
        [IO.Path]::GetFullPath((Join-Path (Split-Path $projectPath -Parent) 'packages.lock.json'))
    } | Sort-Object)
    $actualLockPaths = @($lockFiles | ForEach-Object { $_.FullName } | Sort-Object)
    $lockPathDifferences = @(Compare-Object $expectedLockPaths $actualLockPaths)
    if ($lockPathDifferences.Count -ne 0) {
        $details = ($lockPathDifferences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join '; '
        throw "Solution restore lock topology does not match NexusScholar.Core.slnx: $details"
    }

    function Get-RelativePath([string]$path) {
        $rootUri = [Uri]([IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar)
        $pathUri = [Uri]([IO.Path]::GetFullPath($path))
        return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace([IO.Path]::DirectorySeparatorChar, '/')
    }

    function Get-HashRecord([IO.FileInfo]$file) {
        return [ordered]@{
            path = Get-RelativePath $file.FullName
            sha256 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    function Get-Sha256Hex([byte[]]$bytes) {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha256.ComputeHash($bytes)
        }
        finally {
            $sha256.Dispose()
        }
        return [BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
    }

    $locks = @($lockFiles | ForEach-Object { Get-HashRecord $_ })
    $lockMaterial = [Text.Encoding]::UTF8.GetBytes(($locks | ForEach-Object { "$($_.path)=$($_.sha256)" }) -join "`n")
    $lockDigest = Get-Sha256Hex $lockMaterial

    $artifactFiles = @(Get-ChildItem $releaseDirectory -Recurse -File |
        Where-Object { $_.Name -ne 'release-evidence.json' } |
        Sort-Object FullName)
    $artifacts = @($artifactFiles | ForEach-Object { Get-HashRecord $_ })
    $sdkVersion = (& $dotnet --version).Trim()
    $topologyDigest = (Get-FileHash eng/package-topology.json -Algorithm SHA256).Hash.ToLowerInvariant()

    $provenanceLines = @(
        "commit=$commit"
        "commitTimestampUtc=$commitTimestamp"
        "sdkVersion=$sdkVersion"
        "sbomToolVersion=$toolVersion"
        "topologySha256=$topologyDigest"
        "restoreInputsSha256=$lockDigest"
        "version=$version"
    )
    $provenanceMaterial = [Text.Encoding]::UTF8.GetBytes($provenanceLines -join "`n")
    $provenanceDigest = Get-Sha256Hex $provenanceMaterial

    $evidenceJson = [ordered]@{
        schema = 'nexus.release-evidence.v1'
        version = $version
        commit = $commit
        commitTimestampUtc = $commitTimestamp
        sourceTreeDirty = $dirty
        sdkVersion = $sdkVersion
        sbomTool = [ordered]@{ id = 'Microsoft.Sbom.DotNetTool'; version = $toolVersion }
        topologySha256 = $topologyDigest
        restoreInputsSha256 = $lockDigest
        provenanceInputsSha256 = $provenanceDigest
        signing = 'disabled-by-adr-0024'
        publication = 'validation-only'
        lockFiles = $locks
        artifacts = $artifacts
    } | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText(
        (Join-Path $releaseDirectory 'release-evidence.json'),
        $evidenceJson,
        [Text.UTF8Encoding]::new($false))

    $sourceBinding = if ($dirty) {
        "$commit with sourceTreeDirty=true (validation-only)"
    }
    else {
        "clean commit $commit"
    }
    Write-Host "Release evidence passed: $($artifacts.Count) artifacts and $($lockFiles.Count) lock files generated against $sourceBinding."
}
finally {
    Pop-Location
}
