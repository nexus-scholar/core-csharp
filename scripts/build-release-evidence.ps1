$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
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

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "Tool restore failed with exit code $LASTEXITCODE." }

    $toolVersion = (Get-Content -Raw dotnet-tools.json | ConvertFrom-Json).tools.'microsoft.sbom.dotnettool'.version
    $sbomHost = 'dotnet'
    $sbomPrefix = @('tool', 'run', 'sbom-tool', '--')
    if (-not (dotnet --list-runtimes | Select-String '^Microsoft.NETCore.App 8\.')) {
        $userDotnet = Join-Path $HOME '.dotnet/dotnet.exe'
        if (-not (Test-Path $userDotnet) -or
            -not (& $userDotnet --list-runtimes | Select-String '^Microsoft.NETCore.App 8\.')) {
            throw 'Microsoft.Sbom.DotNetTool requires the .NET 8 runtime. Install the pinned 8.0 runtime before generating evidence.'
        }
        $sbomHost = $userDotnet
        $sbomPrefix = @((Join-Path $HOME ".nuget/packages/microsoft.sbom.dotnettool/$toolVersion/tools/net8.0/any/Microsoft.Sbom.DotNetTool.dll"))
    }

    $commit = (git rev-parse HEAD).Trim()
    $commitTimestamp = [DateTimeOffset]::Parse((git show -s --format=%cI HEAD).Trim()).UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $namespacePart = "$commit/$version"
    & $sbomHost @sbomPrefix Generate `
            -b $releaseDirectory `
            -bc (Join-Path $root 'tests/NexusScholar.PackageSmoke') `
            -pn NexusScholar.Core `
            -pv $version `
            -ps 'Organization: Nexus Scholar' `
            -nsb 'https://github.com/nexus-scholar/core-csharp' `
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
    if ($lockFiles.Count -ne 30) {
        throw "Expected exactly 30 solution restore lock files, found $($lockFiles.Count)."
    }

    function Get-RelativePath([string]$path) {
        return [IO.Path]::GetRelativePath($root, $path).Replace('\\', '/')
    }

    function Get-HashRecord([IO.FileInfo]$file) {
        return [ordered]@{
            path = Get-RelativePath $file.FullName
            sha256 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $locks = @($lockFiles | ForEach-Object { Get-HashRecord $_ })
    $lockMaterial = [Text.Encoding]::UTF8.GetBytes(($locks | ForEach-Object { "$($_.path)=$($_.sha256)" }) -join "`n")
    $lockDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($lockMaterial)).ToLowerInvariant()

    $artifactFiles = @(Get-ChildItem $releaseDirectory -Recurse -File |
        Where-Object { $_.Name -ne 'release-evidence.json' } |
        Sort-Object FullName)
    $artifacts = @($artifactFiles | ForEach-Object { Get-HashRecord $_ })
    $sdkVersion = (dotnet --version).Trim()
    $topologyDigest = (Get-FileHash eng/package-topology.json -Algorithm SHA256).Hash.ToLowerInvariant()
    $dirty = -not [string]::IsNullOrWhiteSpace((git status --porcelain --untracked-files=no | Out-String))

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
    $provenanceDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($provenanceMaterial)).ToLowerInvariant()

    [ordered]@{
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
    } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $releaseDirectory 'release-evidence.json') -Encoding utf8NoBOM

    Write-Host "Release evidence passed: $($artifacts.Count) artifacts and $($lockFiles.Count) lock files bound to $commit."
}
finally {
    Pop-Location
}
