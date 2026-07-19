param(
    [string] $OutputDirectory = 'artifacts/desktop-release',
    [switch] $RequireCleanSourceTree
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

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
        throw "Desktop verification paths must remain under '$artifactsRoot'."
    }
    return $resolved
}

function Get-RelativePath([string] $base, [string] $path) {
    return [IO.Path]::GetRelativePath($base, $path).Replace('\', '/')
}

if (-not $IsWindows) {
    throw 'The extracted Windows desktop artifact must be executed on Windows.'
}

$output = Resolve-ArtifactPath $OutputDirectory
$smokeRoot = Resolve-ArtifactPath 'artifacts/desktop-smoke'
$extractRoot = Join-Path $smokeRoot 'extracted'

Push-Location $root
try {
    $policy = Get-Content -LiteralPath 'eng/desktop-distribution.json' -Raw | ConvertFrom-Json
    $manifestPath = Join-Path $output $policy.manifest
    $checksumsPath = Join-Path $output $policy.checksums
    $sbomPath = Join-Path $output $policy.sbom
    $sbomValidationPath = Join-Path $output $policy.sbomValidation
    $expectedReleaseAssets = @(
        $policy.archive
        $policy.manifest
        $policy.checksums
        $policy.sbom
        $policy.sbomValidation
    )
    foreach ($required in @($manifestPath, $checksumsPath, $sbomPath, $sbomValidationPath)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Desktop release evidence is missing '$required'."
        }
    }

    $actualReleaseFiles = @(
        Get-ChildItem -LiteralPath $output -Recurse -File |
            ForEach-Object { Get-RelativePath $output $_.FullName } |
            Sort-Object
    )
    if (Compare-Object @($expectedReleaseAssets | Sort-Object) $actualReleaseFiles) {
        throw 'Desktop release evidence contains missing, nested, or unexpected files.'
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $headCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the commit used for desktop verification.'
    }
    if ($manifest.schema -cne 'nexus.desktop-distribution-manifest.v1' -or
        $manifest.version -cne $policy.version -or
        $manifest.commit -cne $headCommit -or
        $manifest.runtimeIdentifier -cne $policy.runtimeIdentifier -or
        $manifest.framework -cne $policy.framework -or
        -not $manifest.selfContained -or
        [bool]$manifest.singleFile -ne [bool]$policy.singleFile -or
        [bool]$manifest.trimmed -ne [bool]$policy.trimmed -or
        [bool]$manifest.readyToRun -ne [bool]$policy.readyToRun -or
        $manifest.executable -cne $policy.executable -or
        -not $manifest.unsigned) {
        throw 'Desktop distribution manifest does not match ADR 0046 policy.'
    }
    if ($manifest.PSObject.Properties.Name -notcontains 'sourceTreeDirty' -or
        ($RequireCleanSourceTree -and [bool]$manifest.sourceTreeDirty)) {
        throw 'Desktop distribution manifest does not prove a clean source tree.'
    }
    if ([string]::IsNullOrWhiteSpace($manifest.sdkVersion) -or
        $manifest.restoreInputsSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        (Compare-Object @($policy.nonClaims | Sort-Object) @($manifest.nonClaims | Sort-Object))) {
        throw 'Desktop distribution manifest provenance or nonclaims do not match policy.'
    }
    if (Compare-Object @($expectedReleaseAssets | Sort-Object) @($manifest.releaseAssets | Sort-Object)) {
        throw 'Desktop distribution manifest release assets do not match policy.'
    }

    $checksummedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in Get-Content -LiteralPath $checksumsPath) {
        if ($line -notmatch '^([0-9a-f]{64})  (.+)$') {
            throw "Malformed SHA256SUMS entry: '$line'."
        }
        $expected = $Matches[1]
        $relative = $Matches[2]
        if ([IO.Path]::IsPathFullyQualified($relative) -or
            $relative.Split([char[]]@('/', '\')) -contains '..') {
            throw "Unsafe checksum path '$relative'."
        }
        if (-not $checksummedPaths.Add($relative)) {
            throw "Duplicate SHA256SUMS entry: '$relative'."
        }
        $path = [IO.Path]::GetFullPath((Join-Path $output $relative))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Checksummed desktop release file is missing: '$relative'."
        }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne $expected) {
            throw "Desktop release checksum mismatch: '$relative'."
        }
    }
    $expectedChecksummedPaths = @(
        $policy.archive
        $policy.manifest
        $policy.sbom
        $policy.sbomValidation
    )
    if (Compare-Object @($expectedChecksummedPaths | Sort-Object) @($checksummedPaths | Sort-Object)) {
        throw 'SHA256SUMS does not cover the exact downloadable release assets.'
    }

    $archivePath = Join-Path $output $manifest.archive.path
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw 'Desktop release archive is missing.'
    }
    $archiveFile = Get-Item -LiteralPath $archivePath
    $archiveDigest = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($archiveFile.Length -ne [long]$manifest.archive.length -or
        $archiveDigest -cne $manifest.archive.sha256) {
        throw 'Desktop release archive identity does not match its manifest.'
    }
    if ($archiveFile.Length -gt [long]$policy.maximumArchiveBytes) {
        throw 'Desktop release archive exceeds the admitted size.'
    }

    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

    Add-Type -AssemblyName System.IO.Compression
    $stream = [IO.File]::OpenRead($archivePath)
    try {
        $archive = [IO.Compression.ZipArchive]::new(
            $stream,
            [IO.Compression.ZipArchiveMode]::Read,
            $false,
            [Text.Encoding]::UTF8)
        try {
            $expectedEntries = @{}
            foreach ($file in @($manifest.files)) {
                $entryName = "$($manifest.archiveRoot)/$($file.path)"
                if ($expectedEntries.ContainsKey($entryName)) {
                    throw "Duplicate expected archive path '$entryName'."
                }
                $expectedEntries[$entryName] = $file
            }

            $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            $totalBytes = 0L
            foreach ($entry in $archive.Entries) {
                $name = $entry.FullName.Replace('\', '/')
                if ([string]::IsNullOrWhiteSpace($name) -or
                    $name.StartsWith('/', [StringComparison]::Ordinal) -or
                    [IO.Path]::IsPathFullyQualified($name) -or
                    $name.Split('/') -contains '..' -or
                    -not $seen.Add($name)) {
                    throw "Unsafe or duplicate desktop archive entry '$name'."
                }
                if (-not $expectedEntries.ContainsKey($name)) {
                    throw "Unexpected desktop archive entry '$name'."
                }
                $totalBytes += $entry.Length
                if ($totalBytes -gt [long]$policy.maximumExtractedBytes) {
                    throw 'Desktop archive exceeds the admitted extracted size.'
                }

                $destination = [IO.Path]::GetFullPath((Join-Path $extractRoot $name))
                $extractPrefix = $extractRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
                if (-not $destination.StartsWith($extractPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Desktop archive entry escapes extraction root: '$name'."
                }
                New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                $input = $entry.Open()
                try {
                    $file = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    try {
                        $input.CopyTo($file)
                    }
                    finally {
                        $file.Dispose()
                    }
                }
                finally {
                    $input.Dispose()
                }
            }
            if ($seen.Count -ne $expectedEntries.Count) {
                throw 'Desktop archive is missing one or more manifested files.'
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    $applicationRoot = Join-Path $extractRoot $manifest.archiveRoot
    foreach ($expected in @($manifest.files)) {
        $path = [IO.Path]::GetFullPath((Join-Path $applicationRoot $expected.path))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Extracted desktop file is missing: '$($expected.path)'."
        }
        $file = Get-Item -LiteralPath $path
        $digest = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($file.Length -ne [long]$expected.length -or $digest -cne $expected.sha256) {
            throw "Extracted desktop file failed identity verification: '$($expected.path)'."
        }
    }

    $releaseIdentityPath = Join-Path $applicationRoot 'release-identity.json'
    if (-not (Test-Path -LiteralPath $releaseIdentityPath -PathType Leaf)) {
        throw 'Extracted desktop release identity is missing.'
    }
    $releaseIdentity = Get-Content -LiteralPath $releaseIdentityPath -Raw | ConvertFrom-Json
    if ($releaseIdentity.schema -cne 'nexus.desktop-release-identity.v1' -or
        $releaseIdentity.version -cne $manifest.version -or
        $releaseIdentity.commit -cne $manifest.commit -or
        $releaseIdentity.commitTimestampUtc -cne $manifest.commitTimestampUtc -or
        $releaseIdentity.runtimeIdentifier -cne $manifest.runtimeIdentifier -or
        $releaseIdentity.framework -cne $manifest.framework -or
        [bool]$releaseIdentity.selfContained -ne [bool]$manifest.selfContained -or
        [bool]$releaseIdentity.singleFile -ne [bool]$manifest.singleFile -or
        [bool]$releaseIdentity.trimmed -ne [bool]$manifest.trimmed -or
        [bool]$releaseIdentity.readyToRun -ne [bool]$manifest.readyToRun -or
        [bool]$releaseIdentity.unsigned -ne [bool]$manifest.unsigned -or
        $releaseIdentity.sdkVersion -cne $manifest.sdkVersion -or
        $releaseIdentity.restoreInputsSha256 -cne $manifest.restoreInputsSha256 -or
        (Compare-Object @($releaseIdentity.nonClaims | Sort-Object) @($manifest.nonClaims | Sort-Object))) {
        throw 'Extracted desktop release identity does not match its distribution manifest.'
    }

    $executable = Join-Path $applicationRoot $manifest.executable
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable
    $startInfo.WorkingDirectory = $applicationRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('--release-smoke')
    $startInfo.Environment.Clear()
    foreach ($name in @('SystemRoot', 'WINDIR', 'TEMP', 'TMP', 'ComSpec')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) {
            $startInfo.Environment[$name] = $value
        }
    }
    $startInfo.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
    $startInfo.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $process = [Diagnostics.Process]::Start($startInfo)
    if (-not $process.WaitForExit(90000)) {
        $process.Kill($true)
        throw 'Published desktop smoke did not exit within 90 seconds.'
    }
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    if ($process.ExitCode -ne 0) {
        throw "Published desktop smoke failed with exit code $($process.ExitCode): $stderr"
    }
    $jsonLine = @($stdout -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[-1]
    $smoke = $jsonLine | ConvertFrom-Json
    if ($smoke.schema -cne 'nexus.desktop.release-smoke.v1' -or
        $smoke.status -cne 'passed' -or
        $smoke.version -cne $policy.version -or
        [string]::IsNullOrWhiteSpace($smoke.framework) -or
        -not $smoke.framework.StartsWith('.NET 10.', [StringComparison]::Ordinal) -or
        $smoke.runtimeIdentifier -cne $policy.runtimeIdentifier -or
        $smoke.architecture -cne 'x64' -or
        $smoke.workspaceState -cne 'ReviewReady' -or
        [int]$smoke.inputCount -ne 1 -or
        [int]$smoke.importedRecordCount -ne 1 -or
        -not $smoke.localWorkflowCompleted -or
        $smoke.liveProviderCapabilityLoaded) {
        throw 'Published desktop smoke returned an invalid result.'
    }

    Write-Host "Desktop portable verification passed: $($manifest.files.Count) files, clean extracted smoke, version $($policy.version)."
}
finally {
    Pop-Location
}
