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

Write-Host "Release policy verified: $($approvedPackages.Count) approved packages at $packageVersion; SDK, metadata, and version identity are pinned."
