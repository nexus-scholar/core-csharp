param(
    [string] $ArtifactDirectory = 'artifacts/desktop-release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Gh([string[]] $Arguments) {
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI command failed: gh $($Arguments -join ' ')"
    }
    return $output
}

function Get-ReleaseAssetNames([string] $repository, [string] $tag) {
    return @(
        Invoke-Gh @(
            'api',
            "repos/$repository/releases/tags/$tag",
            '--jq',
            '.assets[].name'
        )
    )
}

if ($env:GITHUB_REF_TYPE -cne 'tag' -or [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
    throw 'GitHub prerelease publication is admitted only from a tag workflow.'
}

$artifactRoot = if ([IO.Path]::IsPathFullyQualified($ArtifactDirectory)) {
    [IO.Path]::GetFullPath($ArtifactDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $root $ArtifactDirectory))
}
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$allowedPrefix = $allowedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $artifactRoot.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Release assets must remain under '$allowedRoot'."
}

Push-Location $root
try {
    $policy = Get-Content -LiteralPath 'eng/desktop-distribution.json' -Raw | ConvertFrom-Json
    $tag = $env:GITHUB_REF_NAME
    $expectedTag = "v$($policy.version)"
    if ($tag -cne $expectedTag) {
        throw "Tag '$tag' does not match desktop version '$($policy.version)'."
    }

    & "$PSScriptRoot/verify-release-tag.ps1" -TagName $tag

    $headCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the release commit.'
    }
    $tagCommit = (& git rev-list -n 1 $tag).Trim()
    if ($LASTEXITCODE -ne 0 -or $tagCommit -cne $headCommit) {
        throw 'The release tag does not resolve to the checked-out commit.'
    }

    & git fetch origin main --no-tags
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to fetch protected main for release binding.'
    }
    $mainCommit = (& git rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $mainCommit -cne $headCommit) {
        throw 'The release tag must resolve to the current protected-main commit.'
    }

    $assetNames = @(
        $policy.archive
        $policy.manifest
        $policy.checksums
        $policy.sbom
        $policy.sbomValidation
    )
    $assetPaths = @($assetNames | ForEach-Object {
        $path = Join-Path $artifactRoot $_
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required release asset is missing: '$_'."
        }
        $path
    })
    $manifest = Get-Content -LiteralPath (Join-Path $artifactRoot $policy.manifest) -Raw |
        ConvertFrom-Json
    if ($manifest.schema -cne 'nexus.desktop-distribution-manifest.v1' -or
        $manifest.version -cne $policy.version -or
        $manifest.commit -cne $headCommit -or
        $manifest.PSObject.Properties.Name -notcontains 'sourceTreeDirty' -or
        [bool]$manifest.sourceTreeDirty -or
        $manifest.runtimeIdentifier -cne $policy.runtimeIdentifier -or
        $manifest.framework -cne $policy.framework -or
        -not $manifest.selfContained -or
        [bool]$manifest.singleFile -ne [bool]$policy.singleFile -or
        [bool]$manifest.trimmed -ne [bool]$policy.trimmed -or
        [bool]$manifest.readyToRun -ne [bool]$policy.readyToRun -or
        -not $manifest.unsigned -or
        (Compare-Object @($assetNames | Sort-Object) @($manifest.releaseAssets | Sort-Object)) -or
        (Compare-Object @($policy.nonClaims | Sort-Object) @($manifest.nonClaims | Sort-Object))) {
        throw 'Desktop distribution manifest is not bound to this clean protected-main release.'
    }

    $repository = if ($env:GITHUB_REPOSITORY) {
        $env:GITHUB_REPOSITORY
    }
    else {
        (Invoke-Gh @('repo', 'view', '--json', 'nameWithOwner', '--jq', '.nameWithOwner')).Trim()
    }
    if ([string]::IsNullOrWhiteSpace($repository)) {
        throw 'Unable to resolve the GitHub repository identity.'
    }

    $releaseNotes = [IO.Path]::GetFullPath((Join-Path $root $policy.releaseNotes))
    if (-not (Test-Path -LiteralPath $releaseNotes -PathType Leaf)) {
        throw 'Version-specific release notes are missing.'
    }
    $title = "Nexus Scholar Desktop $($policy.version) Technical Preview"
    $existingReleaseJson = & gh api "repos/$repository/releases/tags/$tag" 2>$null
    $releaseExists = $LASTEXITCODE -eq 0
    if ($releaseExists) {
        $existingRelease = ($existingReleaseJson | Out-String) | ConvertFrom-Json
        $expectedNotes = (Get-Content -LiteralPath $releaseNotes -Raw).Replace("`r`n", "`n").TrimEnd()
        $actualNotes = ([string]$existingRelease.body).Replace("`r`n", "`n").TrimEnd()
        $existingAssets = @($existingRelease.assets.name | Sort-Object)
        if ($existingRelease.tag_name -cne $tag -or
            $existingRelease.name -cne $title -or
            -not $existingRelease.prerelease -or
            $existingRelease.draft -or
            $actualNotes -cne $expectedNotes -or
            (Compare-Object @($assetNames | Sort-Object) $existingAssets)) {
            throw 'An existing GitHub release is immutable and does not match the admitted prerelease.'
        }
    }
    elseif ($LASTEXITCODE -eq 1) {
        $createArguments = @(
            'release',
            'create',
            $tag,
            '--repo',
            $repository,
            '--verify-tag',
            '--target',
            $headCommit,
            '--title',
            $title,
            '--notes-file',
            $releaseNotes,
            '--prerelease'
        ) + $assetPaths
        Invoke-Gh $createArguments | Out-Null
    }
    else {
        throw 'Unable to inspect the existing GitHub release.'
    }

    $publishedAssets = @(Get-ReleaseAssetNames $repository $tag | Sort-Object)
    if (Compare-Object @($assetNames | Sort-Object) $publishedAssets) {
        throw 'The GitHub prerelease does not contain the exact admitted asset set.'
    }

    $verificationRoot = Join-Path $allowedRoot 'github-release-verification'
    if (Test-Path -LiteralPath $verificationRoot) {
        Remove-Item -LiteralPath $verificationRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
    Invoke-Gh @(
        'release',
        'download',
        $tag,
        '--repo',
        $repository,
        '--dir',
        $verificationRoot
    ) | Out-Null

    $downloadedNames = @(
        Get-ChildItem -LiteralPath $verificationRoot -File |
            Select-Object -ExpandProperty Name |
            Sort-Object
    )
    if (Compare-Object @($assetNames | Sort-Object) $downloadedNames) {
        throw 'Downloaded prerelease assets do not match the admitted asset set.'
    }
    foreach ($assetName in $assetNames) {
        $localDigest = (Get-FileHash -LiteralPath (Join-Path $artifactRoot $assetName) -Algorithm SHA256).Hash
        $downloadedDigest = (Get-FileHash -LiteralPath (Join-Path $verificationRoot $assetName) -Algorithm SHA256).Hash
        if ($downloadedDigest -cne $localDigest) {
            throw "Downloaded prerelease asset differs from the validated local artifact: '$assetName'."
        }
    }
    foreach ($line in Get-Content -LiteralPath (Join-Path $verificationRoot $policy.checksums)) {
        if ($line -notmatch '^([0-9a-f]{64})  (.+)$') {
            throw "Malformed published checksum entry: '$line'."
        }
        $path = Join-Path $verificationRoot $Matches[2]
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne $Matches[1]) {
            throw "Downloaded prerelease asset failed checksum verification: '$($Matches[2])'."
        }
    }

    $release = Invoke-Gh @(
        'api',
        "repos/$repository/releases/tags/$tag",
        '--jq',
        '{url:.html_url,prerelease:.prerelease,draft:.draft,tag:.tag_name}'
    ) | ConvertFrom-Json
    if (-not $release.prerelease -or $release.draft -or $release.tag -cne $tag) {
        throw 'Published GitHub release identity is invalid.'
    }
    Write-Host "GitHub prerelease verified: $($release.url)"
}
finally {
    Pop-Location
}
