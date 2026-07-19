$ErrorActionPreference = 'Stop'

$projects = Get-ChildItem src -Recurse -Filter *.csproj
if ($projects.Count -eq 0) { throw 'No source projects were found.' }

$props = [xml](Get-Content -Raw Directory.Build.props)
if ($props.Project.PropertyGroup.IsPackable -ne 'false') {
    throw 'Directory.Build.props must default IsPackable to false.'
}
if ($props.Project.PropertyGroup.PackageLicenseExpression -ne 'MIT' -or -not (Test-Path LICENSE)) {
    throw 'MIT package metadata and the repository LICENSE file are required.'
}

$sdk = Get-Content -Raw global.json | ConvertFrom-Json
if ($sdk.sdk.rollForward -ne 'disable' -or $sdk.sdk.allowPrerelease -ne $false) {
    throw 'global.json must pin the stable SDK without feature-band roll-forward.'
}

$topology = Get-Content -Raw eng/package-topology.json | ConvertFrom-Json
$packageVersion = "$($props.Project.PropertyGroup.VersionPrefix)-$($props.Project.PropertyGroup.VersionSuffix)"
if ($topology.version -ne $packageVersion) {
    throw "Package topology version '$($topology.version)' does not match Directory.Build.props version '$packageVersion'."
}

$desktopPolicy = Get-Content -Raw eng/desktop-distribution.json | ConvertFrom-Json
if ($desktopPolicy.schema -cne 'nexus.desktop-distribution-policy.v1' -or
    $desktopPolicy.version -cne $packageVersion) {
    throw 'Desktop distribution policy schema or version does not match repository version metadata.'
}
if ($desktopPolicy.project -cne 'src/NexusScholar.Desktop/NexusScholar.Desktop.csproj' -or
    $desktopPolicy.runtimeIdentifier -cne 'win-x64' -or
    $desktopPolicy.framework -cne 'net10.0' -or
    -not $desktopPolicy.selfContained -or
    $desktopPolicy.singleFile -or
    $desktopPolicy.trimmed -or
    $desktopPolicy.readyToRun -or
    -not $desktopPolicy.unsigned) {
    throw 'Desktop distribution policy must remain unsigned, self-contained win-x64 net10.0 portable output.'
}
$expectedArchive = "NexusScholar-Desktop-$packageVersion-win-x64.zip"
$expectedSbom = "NexusScholar-Desktop-$packageVersion-win-x64.spdx.json"
if ($desktopPolicy.archive -cne $expectedArchive -or
    $desktopPolicy.sbom -cne $expectedSbom -or
    $desktopPolicy.manifest -cne 'desktop-distribution-manifest.json' -or
    $desktopPolicy.checksums -cne 'SHA256SUMS.txt' -or
    $desktopPolicy.sbomValidation -cne 'sbom-validation.json') {
    throw 'Desktop release asset names do not match the admitted version identity.'
}
$desktopAssetNames = @(
    $desktopPolicy.archive
    $desktopPolicy.manifest
    $desktopPolicy.checksums
    $desktopPolicy.sbom
    $desktopPolicy.sbomValidation
)
if (@($desktopAssetNames | Sort-Object -Unique).Count -ne $desktopAssetNames.Count) {
    throw 'Desktop release asset names must be unique.'
}
foreach ($assetName in $desktopAssetNames) {
    if ([IO.Path]::GetFileName($assetName) -cne $assetName) {
        throw "Desktop release asset '$assetName' must be a root-level file name."
    }
}
if (-not (Test-Path -LiteralPath $desktopPolicy.releaseNotes -PathType Leaf)) {
    throw 'Version-specific desktop release notes are required.'
}

$desktopLockFiles = @(
    Get-ChildItem -LiteralPath $desktopPolicy.lockDirectory -File -Filter '*.packages.lock.json' |
        Sort-Object Name
)
$expectedDesktopLockNames = @(
    $desktopPolicy.lockProjects |
        ForEach-Object { "$_.packages.lock.json" } |
        Sort-Object
)
if (Compare-Object $expectedDesktopLockNames @($desktopLockFiles.Name | Sort-Object)) {
    throw 'Desktop RID lock topology does not match eng/desktop-distribution.json.'
}
foreach ($lockFile in $desktopLockFiles) {
    $lock = Get-Content -LiteralPath $lockFile.FullName -Raw | ConvertFrom-Json
    if ($lock.version -ne 2 -or
        $lock.dependencies.PSObject.Properties.Name -notcontains 'net10.0/win-x64') {
        throw "Desktop RID lock '$($lockFile.Name)' does not bind net10.0/win-x64."
    }
}
$desktopLockProperty = $props.Project.PropertyGroup.NuGetLockFilePath
if ($desktopLockProperty.InnerText -cne
        '$(MSBuildThisFileDirectory)eng/desktop-locks/$(MSBuildProjectName).packages.lock.json' -or
    $desktopLockProperty.Condition -cne "'`$(DesktopReleaseRestore)' == 'true'") {
    throw 'Directory.Build.props must isolate desktop RID locks from normal project lock files.'
}

$desktopProjectPath = Join-Path (Get-Location) $desktopPolicy.project
$desktopProject = [xml](Get-Content -Raw $desktopProjectPath)
if ($desktopProject.Project.PropertyGroup.IsPackable -notcontains 'false' -or
    $desktopProject.Project.PropertyGroup.ApplicationIcon -notcontains 'Assets/NexusScholar.ico' -or
    -not (Test-Path -LiteralPath 'src/NexusScholar.Desktop/Assets/NexusScholar.ico' -PathType Leaf)) {
    throw 'The desktop project must remain non-packable and carry its admitted Windows icon.'
}

$releaseWorkflowLines = @(Get-Content .github/workflows/release-validation.yml)
$publishStart = [Array]::IndexOf($releaseWorkflowLines, '  publish-prerelease:')
if ($publishStart -lt 0) {
    throw 'Release workflow is missing the publish-prerelease job.'
}
$publishEnd = $releaseWorkflowLines.Count
for ($index = $publishStart + 1; $index -lt $releaseWorkflowLines.Count; $index++) {
    if ($releaseWorkflowLines[$index] -match '^  [A-Za-z0-9_-]+:\s*$') {
        $publishEnd = $index
        break
    }
}
$publishBlock = @(
    $releaseWorkflowLines[$publishStart..($publishEnd - 1)] |
        Where-Object { $_ -notmatch '^\s*#' }
)
$releaseWorkflow = ($releaseWorkflowLines | Where-Object { $_ -notmatch '^\s*#' }) -join "`n"
$publicationCondition = "if: github.ref_type == 'tag' && github.ref_name == format('v{0}', needs.validate-core.outputs.version)"
$publishPermissionsIndex = [Array]::IndexOf($publishBlock, '    permissions:')
$expectedPublishLines = @(
    "    $publicationCondition"
    '    environment: release'
    '      contents: write'
    '      id-token: write'
    '      attestations: write'
    '        run: ./scripts/publish-github-prerelease.ps1'
)
$cleanDesktopVerificationLine =
    '        run: ./scripts/verify-desktop-portable.ps1 -RequireCleanSourceTree'
$reverifyIndex = [Array]::IndexOf(
    $publishBlock,
    '      - name: Reverify extracted desktop artifact')
$firstAttestationIndex = [Array]::IndexOf(
    $publishBlock,
    '      - name: Attest core validation evidence')
if ($publishPermissionsIndex -lt 0 -or
    @($expectedPublishLines | Where-Object { $publishBlock -cnotcontains $_ }).Count -ne 0 -or
    @($publishBlock | Where-Object { $_ -ceq "    $publicationCondition" }).Count -ne 1 -or
    @($releaseWorkflowLines | Where-Object { $_ -ceq $cleanDesktopVerificationLine }).Count -lt 2 -or
    $reverifyIndex -lt 0 -or
    $firstAttestationIndex -lt 0 -or
    $reverifyIndex -gt $firstAttestationIndex -or
    ([regex]::Matches($releaseWorkflow, '(?m)^\s+contents: write\s*$')).Count -ne 1 -or
    $releaseWorkflow -match '(?i)(dotnet\s+nuget\s+push|nuget\s+push)') {
    throw 'Release workflow publication must remain clean-verified, pre-attestation, exact-tag-only, release-environment-scoped, and free of NuGet publication.'
}

$desktopVerifier = Get-Content -LiteralPath scripts/verify-desktop-portable.ps1 -Raw
if ($desktopVerifier -cnotmatch [regex]::Escape(
        '($RequireCleanSourceTree -and [bool]$manifest.sourceTreeDirty)') -or
    $desktopVerifier -cnotmatch [regex]::Escape('$manifest.commit -cne $headCommit') -or
    $desktopVerifier -cnotmatch [regex]::Escape(
        'Desktop release evidence contains missing, nested, or unexpected files.')) {
    throw 'Desktop verification must bind the exact root asset set to the checked-out clean source commit.'
}

$publicationScript = Get-Content -LiteralPath scripts/publish-github-prerelease.ps1 -Raw
if ($publicationScript -match '(?is)(release\s+(delete|edit|upload)\b|[''"]release[''"]\s*,\s*[''"](delete|edit|upload)[''"]|--clobber\b|--method\s+DELETE\b)' -or
    $publicationScript -cnotmatch [regex]::Escape('if ($releaseExists)') -or
    $publicationScript -cnotmatch [regex]::Escape('An existing GitHub release is immutable and does not match the admitted prerelease.') -or
    $publicationScript -cnotmatch [regex]::Escape('Downloaded prerelease asset differs from the validated local artifact:')) {
    throw 'GitHub prerelease publication must treat an existing release as immutable.'
}

$smokeProject = [xml](Get-Content -Raw tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj)
$smokeVersions = @($smokeProject.Project.ItemGroup.PackageReference | ForEach-Object { $_.Version } | Sort-Object -Unique)
if ($smokeVersions.Count -ne 1 -or $smokeVersions[0] -ne $packageVersion) {
    throw 'Package smoke references must exactly match the active package version.'
}
$smokePackageIds = @($smokeProject.Project.ItemGroup.PackageReference | ForEach-Object { $_.Include } | Sort-Object)
$smokeRoots = @($topology.smokeRoots | Sort-Object)
if (Compare-Object $smokeRoots $smokePackageIds) {
    throw 'Package smoke references do not match eng/package-topology.json smokeRoots.'
}

$tagName = "v$packageVersion"
& git show-ref --verify --quiet "refs/tags/$tagName"
$tagStatus = $LASTEXITCODE
if ($tagStatus -eq 0) {
    $tagCommit = & git rev-list -n 1 $tagName
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve package tag '$tagName'." }
    $headCommit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the current Git commit.' }
    if ($tagCommit.Trim() -ne $headCommit.Trim()) {
        throw "Package version '$packageVersion' is already tagged at another commit. Bump the package version before packing."
    }
}
elseif ($tagStatus -ne 1) {
    throw "Unable to inspect package tag '$tagName'."
}

$approvedPackages = @($topology.packages | Sort-Object)
$packableProjects = foreach ($project in $projects) {
    $xml = [xml](Get-Content -Raw $project.FullName)
    if ($xml.Project.PropertyGroup.IsPackable -contains 'true') { $project.BaseName }
}
if (Compare-Object $approvedPackages @($packableProjects | Sort-Object)) {
    throw 'Packable source projects do not match eng/package-topology.json.'
}

Write-Host "Release policy verified: $($approvedPackages.Count) validation packages and one exact-tag-only $($desktopPolicy.runtimeIdentifier) desktop preview at $packageVersion."
