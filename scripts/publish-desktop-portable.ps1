param(
    [string] $OutputDirectory = 'artifacts/desktop-release',
    [switch] $RequireCleanSourceTree
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/resolve-dotnet.ps1"
$dotnet = Use-PinnedDotNet $root

function Resolve-ArtifactPath([string] $path) {
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    $resolved = if ([IO.Path]::IsPathFullyQualified($path)) {
        [IO.Path]::GetFullPath($path)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $root $path))
    }
    $prefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Desktop release output must remain under '$artifactsRoot'."
    }
    return $resolved
}

function Write-Utf8Json([string] $path, $value) {
    $json = $value | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($path, $json, [Text.UTF8Encoding]::new($false))
}

function Get-RelativePath([string] $base, [string] $path) {
    return [IO.Path]::GetRelativePath($base, $path).Replace('\', '/')
}

function Get-FileInventory([string] $directory) {
    return @(
        Get-ChildItem -LiteralPath $directory -Recurse -File |
            Sort-Object { Get-RelativePath $directory $_.FullName } |
            ForEach-Object {
                [ordered]@{
                    path = Get-RelativePath $directory $_.FullName
                    length = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
}

function Get-Sha256Hex([string] $material) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($material)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Add-ReleaseFiles(
    [string] $publishDirectory,
    $policy,
    [string] $commit,
    [string] $commitTimestampUtc,
    [string] $sdkVersion,
    [string] $restoreInputsDigest) {
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $publishDirectory 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $root $policy.releaseNotes) -Destination (Join-Path $publishDirectory 'TECHNICAL-PREVIEW-NOTES.md')
    Write-Utf8Json (Join-Path $publishDirectory 'release-identity.json') ([ordered]@{
        schema = 'nexus.desktop-release-identity.v1'
        version = $policy.version
        commit = $commit
        commitTimestampUtc = $commitTimestampUtc
        runtimeIdentifier = $policy.runtimeIdentifier
        framework = $policy.framework
        selfContained = [bool]$policy.selfContained
        singleFile = [bool]$policy.singleFile
        trimmed = [bool]$policy.trimmed
        readyToRun = [bool]$policy.readyToRun
        unsigned = [bool]$policy.unsigned
        sdkVersion = $sdkVersion
        restoreInputsSha256 = $restoreInputsDigest
        nonClaims = @($policy.nonClaims)
    })
}

function New-DeterministicArchive(
    [string] $sourceDirectory,
    [string] $archivePath,
    [string] $rootName,
    [DateTimeOffset] $timestamp) {
    Add-Type -AssemblyName System.IO.Compression
    $normalizedTimestamp = $timestamp.ToUniversalTime()
    if ($normalizedTimestamp.Year -lt 1980) {
        $normalizedTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    }
    $normalizedTimestamp = [DateTimeOffset]::new(
        $normalizedTimestamp.Year,
        $normalizedTimestamp.Month,
        $normalizedTimestamp.Day,
        $normalizedTimestamp.Hour,
        $normalizedTimestamp.Minute,
        $normalizedTimestamp.Second - ($normalizedTimestamp.Second % 2),
        [TimeSpan]::Zero)

    $stream = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new(
            $stream,
            [IO.Compression.ZipArchiveMode]::Create,
            $false,
            [Text.Encoding]::UTF8)
        try {
            foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
                     Sort-Object { Get-RelativePath $sourceDirectory $_.FullName }) {
                $relative = Get-RelativePath $sourceDirectory $file.FullName
                $entry = $archive.CreateEntry(
                    "$rootName/$relative",
                    [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $normalizedTimestamp
                $entryStream = $entry.Open()
                try {
                    $input = [IO.File]::OpenRead($file.FullName)
                    try {
                        $input.CopyTo($entryStream)
                    }
                    finally {
                        $input.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

if (-not $IsWindows) {
    throw 'The desktop portable release must be built and executed on Windows.'
}

$output = Resolve-ArtifactPath $OutputDirectory
$publishRoot = Resolve-ArtifactPath 'artifacts/desktop-publish'
$firstPublish = Join-Path $publishRoot 'first'
$secondPublish = Join-Path $publishRoot 'second'

Push-Location $root
try {
    $policy = Get-Content -LiteralPath 'eng/desktop-distribution.json' -Raw | ConvertFrom-Json
    $props = [xml](Get-Content -LiteralPath 'Directory.Build.props' -Raw)
    $version = "$($props.Project.PropertyGroup.VersionPrefix)-$($props.Project.PropertyGroup.VersionSuffix)"
    if ($policy.schema -cne 'nexus.desktop-distribution-policy.v1' -or $policy.version -cne $version) {
        throw 'Desktop distribution policy and repository version metadata do not match.'
    }
    if ($policy.runtimeIdentifier -cne 'win-x64' -or -not $policy.selfContained -or -not $policy.unsigned) {
        throw 'ADR 0046 admits only an unsigned, self-contained win-x64 portable preview.'
    }

    $status = git status --porcelain --untracked-files=all
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the source tree before desktop publication.'
    }
    $dirty = -not [string]::IsNullOrWhiteSpace(($status | Out-String))
    if ($RequireCleanSourceTree -and $dirty) {
        throw 'Desktop release publication requires a clean source tree.'
    }

    $commit = (git rev-parse HEAD).Trim()
    $commitTimestamp = [DateTimeOffset]::Parse(
        (git show -s --format=%cI HEAD).Trim(),
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
    $commitTimestampUtc = $commitTimestamp.ToString(
        'yyyy-MM-ddTHH:mm:ssZ',
        [Globalization.CultureInfo]::InvariantCulture)
    $sdkVersion = (& $dotnet --version).Trim()
    $lockRoot = [IO.Path]::GetFullPath((Join-Path $root $policy.lockDirectory))
    $lockInventory = Get-FileInventory $lockRoot
    $expectedLockNames = @($policy.lockProjects | ForEach-Object { "$_.packages.lock.json" } | Sort-Object)
    if (Compare-Object $expectedLockNames @($lockInventory.path | Sort-Object)) {
        throw 'Desktop RID lock topology does not match the distribution policy.'
    }
    $restoreInputsMaterial = $lockInventory |
        ForEach-Object { "$($_.path)=$($_.length):$($_.sha256)" }
    $restoreInputsDigest = Get-Sha256Hex ($restoreInputsMaterial -join "`n")

    foreach ($path in @($output, $publishRoot)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }

    & $dotnet restore $policy.project `
        -r $policy.runtimeIdentifier `
        --locked-mode `
        -p:DesktopReleaseRestore=true
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop restore failed with exit code $LASTEXITCODE."
    }

    foreach ($publishDirectory in @($firstPublish, $secondPublish)) {
        New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
        & $dotnet publish $policy.project `
            --configuration Release `
            --runtime $policy.runtimeIdentifier `
            --self-contained true `
            --no-restore `
            --output $publishDirectory `
            -p:ContinuousIntegrationBuild=true `
            -p:DebugSymbols=false `
            -p:DebugType=None `
            -p:PublishSingleFile=false `
            -p:PublishTrimmed=false `
            -p:PublishReadyToRun=false
        if ($LASTEXITCODE -ne 0) {
            throw "Desktop publish failed with exit code $LASTEXITCODE."
        }
        Get-ChildItem -LiteralPath $publishDirectory -Recurse -File -Filter '*.pdb' |
            ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
        Add-ReleaseFiles `
            $publishDirectory `
            $policy `
            $commit `
            $commitTimestampUtc `
            $sdkVersion `
            $restoreInputsDigest
    }

    $firstInventory = Get-FileInventory $firstPublish
    $secondInventory = Get-FileInventory $secondPublish
    $firstMaterial = $firstInventory | ConvertTo-Json -Depth 4 -Compress
    $secondMaterial = $secondInventory | ConvertTo-Json -Depth 4 -Compress
    if ($firstMaterial -cne $secondMaterial) {
        throw 'Repeated desktop publishes produced different file inventories or digests.'
    }
    if ($firstInventory.Count -eq 0 -or -not ($firstInventory.path -contains $policy.executable)) {
        throw "Desktop publish did not produce '$($policy.executable)'."
    }
    if ($firstInventory.path | Where-Object { $_.EndsWith('.pdb', [StringComparison]::OrdinalIgnoreCase) }) {
        throw 'Desktop technical-preview output must not contain portable debug symbols.'
    }

    $archivePath = Join-Path $output $policy.archive
    $archiveRoot = [IO.Path]::GetFileNameWithoutExtension($policy.archive)
    New-DeterministicArchive $firstPublish $archivePath $archiveRoot $commitTimestamp
    $archiveFile = Get-Item -LiteralPath $archivePath
    if ($archiveFile.Length -gt [long]$policy.maximumArchiveBytes) {
        throw "Desktop archive exceeds the admitted maximum size of $($policy.maximumArchiveBytes) bytes."
    }

    $archiveDigest = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestPath = Join-Path $output $policy.manifest
    Write-Utf8Json $manifestPath ([ordered]@{
        schema = 'nexus.desktop-distribution-manifest.v1'
        version = $version
        commit = $commit
        commitTimestampUtc = $commitTimestampUtc
        sourceTreeDirty = $dirty
        runtimeIdentifier = $policy.runtimeIdentifier
        framework = $policy.framework
        selfContained = [bool]$policy.selfContained
        singleFile = [bool]$policy.singleFile
        trimmed = [bool]$policy.trimmed
        readyToRun = [bool]$policy.readyToRun
        unsigned = [bool]$policy.unsigned
        sdkVersion = $sdkVersion
        restoreInputsSha256 = $restoreInputsDigest
        restoreLocks = $lockInventory
        executable = $policy.executable
        archiveRoot = $archiveRoot
        archive = [ordered]@{
            path = $policy.archive
            length = $archiveFile.Length
            sha256 = $archiveDigest
        }
        releaseAssets = @(
            $policy.archive
            $policy.manifest
            $policy.checksums
            $policy.sbom
            $policy.sbomValidation
        )
        files = $firstInventory
        nonClaims = @($policy.nonClaims)
    })

    & $dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "Tool restore failed with exit code $LASTEXITCODE."
    }
    & $dotnet tool run sbom-tool -- Generate `
        -b $output `
        -bc (Join-Path $root 'src/NexusScholar.Desktop') `
        -pn 'Nexus Scholar Desktop' `
        -pv $version `
        -ps 'Organization: Nexus Scholar' `
        -nsb 'https://github.com/nexus-scholar-org/core-csharp' `
        -nsu "$commit/desktop/$version/win-x64" `
        -gt $commitTimestampUtc `
        -D true `
        -F false `
        -V Warning
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop SBOM generation failed with exit code $LASTEXITCODE."
    }
    & $dotnet tool run sbom-tool -- Validate `
        -b $output `
        -o (Join-Path $output $policy.sbomValidation) `
        -n `
        -F false `
        -mi SPDX:2.2 `
        -V Warning
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop SBOM validation failed with exit code $LASTEXITCODE."
    }

    $generatedSbom = Join-Path $output '_manifest/spdx_2.2/manifest.spdx.json'
    if (-not (Test-Path -LiteralPath $generatedSbom -PathType Leaf)) {
        throw 'Desktop SPDX SBOM generation did not produce the expected manifest.'
    }
    Copy-Item -LiteralPath $generatedSbom -Destination (Join-Path $output $policy.sbom)
    Remove-Item -LiteralPath (Join-Path $output '_manifest') -Recurse -Force

    $checksummedAssets = @(
        $policy.archive
        $policy.manifest
        $policy.sbom
        $policy.sbomValidation
    )
    $checksumLines = @(
        $checksummedAssets | Sort-Object | ForEach-Object {
            $path = Join-Path $output $_
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Desktop release asset is missing before checksumming: '$_'."
            }
            $digest = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            "$digest  $_"
        }
    )
    [IO.File]::WriteAllText(
        (Join-Path $output $policy.checksums),
        ($checksumLines -join "`n") + "`n",
        [Text.UTF8Encoding]::new($false))

    Write-Host "Desktop portable release built: $($firstInventory.Count) files, archive $($policy.archive), version $version."
}
finally {
    Pop-Location
}
