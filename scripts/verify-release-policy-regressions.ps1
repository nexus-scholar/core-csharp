$ErrorActionPreference = 'Stop'

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ("nexus-release-policy-" + [Guid]::NewGuid().ToString('N'))
$resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
if (-not $resolvedTempRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release-policy regression workspace escaped the system temporary directory.'
}

function Invoke-Git([string[]]$Arguments) {
    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }
}

try {
    New-Item $tempRoot -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'src/Test.Package') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'src/NexusScholar.Desktop/Assets') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'eng') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'eng/desktop-locks') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'tests/NexusScholar.PackageSmoke') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'scripts') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot '.github/workflows') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'docs/release') -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'verify-release-policy.ps1') (Join-Path $tempRoot 'scripts/verify-release-policy.ps1')
    Copy-Item (Join-Path $PSScriptRoot 'publish-github-prerelease.ps1') (Join-Path $tempRoot 'scripts/publish-github-prerelease.ps1')
    Copy-Item (Join-Path $PSScriptRoot 'verify-desktop-portable.ps1') (Join-Path $tempRoot 'scripts/verify-desktop-portable.ps1')

    @'
<Project>
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <VersionPrefix>0.0.1</VersionPrefix>
    <VersionSuffix>alpha.1</VersionSuffix>
    <NuGetLockFilePath Condition="'$(DesktopReleaseRestore)' == 'true'">$(MSBuildThisFileDirectory)eng/desktop-locks/$(MSBuildProjectName).packages.lock.json</NuGetLockFilePath>
  </PropertyGroup>
</Project>
'@ | Set-Content (Join-Path $tempRoot 'Directory.Build.props') -Encoding utf8
    '{"sdk":{"version":"10.0.100","rollForward":"disable","allowPrerelease":false}}' |
        Set-Content (Join-Path $tempRoot 'global.json') -Encoding utf8
    'test license' | Set-Content (Join-Path $tempRoot 'LICENSE') -Encoding utf8
    '{"version":"0.0.1-alpha.1","packages":["Test.Package"],"smokeRoots":["Test.Package"]}' |
        Set-Content (Join-Path $tempRoot 'eng/package-topology.json') -Encoding utf8
    @'
{
  "schema": "nexus.desktop-distribution-policy.v1",
  "version": "0.0.1-alpha.1",
  "project": "src/NexusScholar.Desktop/NexusScholar.Desktop.csproj",
  "runtimeIdentifier": "win-x64",
  "framework": "net10.0",
  "selfContained": true,
  "singleFile": false,
  "trimmed": false,
  "readyToRun": false,
  "unsigned": true,
  "archive": "NexusScholar-Desktop-0.0.1-alpha.1-win-x64.zip",
  "manifest": "desktop-distribution-manifest.json",
  "checksums": "SHA256SUMS.txt",
  "sbom": "NexusScholar-Desktop-0.0.1-alpha.1-win-x64.spdx.json",
  "sbomValidation": "sbom-validation.json",
  "lockDirectory": "eng/desktop-locks",
  "lockProjects": ["NexusScholar.Desktop"],
  "releaseNotes": "docs/release/notes.md"
}
'@ | Set-Content (Join-Path $tempRoot 'eng/desktop-distribution.json') -Encoding utf8
    '{"version":2,"dependencies":{"net10.0":{},"net10.0/win-x64":{}}}' |
        Set-Content (Join-Path $tempRoot 'eng/desktop-locks/NexusScholar.Desktop.packages.lock.json') -Encoding utf8
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>' |
        Set-Content (Join-Path $tempRoot 'src/Test.Package/Test.Package.csproj') -Encoding utf8
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsPackable>false</IsPackable><ApplicationIcon>Assets/NexusScholar.ico</ApplicationIcon></PropertyGroup></Project>' |
        Set-Content (Join-Path $tempRoot 'src/NexusScholar.Desktop/NexusScholar.Desktop.csproj') -Encoding utf8
    [IO.File]::WriteAllBytes(
        (Join-Path $tempRoot 'src/NexusScholar.Desktop/Assets/NexusScholar.ico'),
        [byte[]](0, 0, 1, 0))
    'technical preview' | Set-Content (Join-Path $tempRoot 'docs/release/notes.md') -Encoding utf8
    @'
name: release-validation
jobs:
  validate-core:
    outputs:
      version: value
  validate-desktop:
    steps:
      - name: Verify
        run: ./scripts/verify-desktop-portable.ps1 -RequireCleanSourceTree
  publish-prerelease:
    if: github.ref_type == 'tag' && github.ref_name == format('v{0}', needs.validate-core.outputs.version)
    environment: release
    permissions:
      contents: write
      id-token: write
      attestations: write
    steps:
      - name: Reverify extracted desktop artifact
        run: ./scripts/verify-desktop-portable.ps1 -RequireCleanSourceTree
      - name: Attest core validation evidence
        uses: actions/attest-build-provenance@v4
      - name: Publish
        run: ./scripts/publish-github-prerelease.ps1
'@ | Set-Content (Join-Path $tempRoot '.github/workflows/release-validation.yml') -Encoding utf8
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="Test.Package" Version="0.0.1-alpha.1" /></ItemGroup></Project>' |
        Set-Content (Join-Path $tempRoot 'tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj') -Encoding utf8

    Push-Location $tempRoot
    try {
        Invoke-Git @('init', '--quiet')
        Invoke-Git @('config', 'user.name', 'Nexus Release Policy Test')
        Invoke-Git @('config', 'user.email', 'release-policy@example.invalid')
        Invoke-Git @('add', '.')
        Invoke-Git @('commit', '--quiet', '-m', 'initial package identity')
        Invoke-Git @('tag', 'v0.0.1-alpha.1')

        & ./scripts/verify-release-policy.ps1

        $desktopPolicyPath = 'eng/desktop-distribution.json'
        $validDesktopPolicy = Get-Content -Raw $desktopPolicyPath
        $invalidDesktopPolicy = $validDesktopPolicy.Replace(
            '"runtimeIdentifier": "win-x64"',
            '"runtimeIdentifier": "linux-x64"')
        $invalidDesktopPolicy | Set-Content $desktopPolicyPath -Encoding utf8
        $wrongRidRejected = $false
        try {
            & ./scripts/verify-release-policy.ps1
        }
        catch {
            if ($_.Exception.Message -like '*self-contained win-x64*') {
                $wrongRidRejected = $true
            }
            else {
                throw
            }
        }
        if (-not $wrongRidRejected) {
            throw 'Release policy accepted a desktop artifact with the wrong RID.'
        }
        $validDesktopPolicy | Set-Content $desktopPolicyPath -Encoding utf8

        $workflowPath = '.github/workflows/release-validation.yml'
        $validWorkflow = Get-Content -Raw $workflowPath
        ("# if: github.ref_type == 'tag' && github.ref_name == format('v{0}', needs.validate-core.outputs.version)`n" +
        $validWorkflow.Replace(
            "if: github.ref_type == 'tag' && github.ref_name == format('v{0}', needs.validate-core.outputs.version)",
            'if: always()')) | Set-Content $workflowPath -Encoding utf8
        $branchPublicationRejected = $false
        try {
            & ./scripts/verify-release-policy.ps1
        }
        catch {
            if ($_.Exception.Message -like '*exact-tag-only*') {
                $branchPublicationRejected = $true
            }
            else {
                throw
            }
        }
        if (-not $branchPublicationRejected) {
            throw 'Release policy accepted branch-capable GitHub publication.'
        }
        $validWorkflow | Set-Content $workflowPath -Encoding utf8

        $publicationScriptPath = 'scripts/publish-github-prerelease.ps1'
        $validPublicationScript = Get-Content -Raw $publicationScriptPath
        $invalidPublicationScript = $validPublicationScript -replace
            "(?s)'release',\s*'create',",
            "'release',`n            'delete',"
        if ($invalidPublicationScript -ceq $validPublicationScript) {
            throw 'Unable to construct the existing-release mutation regression fixture.'
        }
        $invalidPublicationScript | Set-Content $publicationScriptPath -Encoding utf8
        $releaseMutationRejected = $false
        try {
            & ./scripts/verify-release-policy.ps1
        }
        catch {
            if ($_.Exception.Message -like '*existing release as immutable*') {
                $releaseMutationRejected = $true
            }
            else {
                throw
            }
        }
        if (-not $releaseMutationRejected) {
            throw 'Release policy accepted publication that can mutate an existing release.'
        }
        $validPublicationScript | Set-Content $publicationScriptPath -Encoding utf8

        $desktopVerifierPath = 'scripts/verify-desktop-portable.ps1'
        $validDesktopVerifier = Get-Content -Raw $desktopVerifierPath
        $invalidDesktopVerifier = $validDesktopVerifier.Replace(
            '($RequireCleanSourceTree -and [bool]$manifest.sourceTreeDirty)',
            '$false')
        if ($invalidDesktopVerifier -ceq $validDesktopVerifier) {
            throw 'Unable to construct the dirty-desktop-manifest regression fixture.'
        }
        $invalidDesktopVerifier | Set-Content $desktopVerifierPath -Encoding utf8
        $dirtyManifestAccepted = $false
        try {
            & ./scripts/verify-release-policy.ps1
        }
        catch {
            if ($_.Exception.Message -like '*checked-out clean source commit*') {
                $dirtyManifestAccepted = $true
            }
            else {
                throw
            }
        }
        if (-not $dirtyManifestAccepted) {
            throw 'Release policy accepted desktop verification without dirty-manifest rejection.'
        }
        $validDesktopVerifier | Set-Content $desktopVerifierPath -Encoding utf8

        'next commit' | Set-Content marker.txt -Encoding utf8
        Invoke-Git @('add', 'marker.txt')
        Invoke-Git @('commit', '--quiet', '-m', 'move head without version bump')

        $reusedVersionRejected = $false
        try {
            & ./scripts/verify-release-policy.ps1
        }
        catch {
            if ($_.Exception.Message -like '*already tagged at another commit*') {
                $reusedVersionRejected = $true
            }
            else {
                throw
            }
        }
        if (-not $reusedVersionRejected) {
            throw 'Release policy accepted a package version tagged at another commit.'
        }

        (Get-Content -Raw Directory.Build.props).Replace('alpha.1', 'alpha.2') |
            Set-Content Directory.Build.props -Encoding utf8
        (Get-Content -Raw eng/package-topology.json).Replace('alpha.1', 'alpha.2') |
            Set-Content eng/package-topology.json -Encoding utf8
        (Get-Content -Raw eng/desktop-distribution.json).Replace('alpha.1', 'alpha.2') |
            Set-Content eng/desktop-distribution.json -Encoding utf8
        (Get-Content -Raw tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj).Replace('alpha.1', 'alpha.2') |
            Set-Content tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj -Encoding utf8

        & ./scripts/verify-release-policy.ps1
    }
    finally {
        Pop-Location
    }

    Write-Host 'Release policy regressions passed: tag reuse, wrong RID, branch publication, existing-release mutation, and dirty desktop manifests are rejected.'
}
finally {
    if (Test-Path -LiteralPath $resolvedTempRoot) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
