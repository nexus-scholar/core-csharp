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

$unexpectedOverrides = foreach ($project in $projects) {
    $xml = [xml](Get-Content -Raw $project.FullName)
    if ($xml.Project.PropertyGroup.IsPackable -contains 'true') { $project.FullName }
}
if ($unexpectedOverrides) {
    throw "Projects became packable without an accepted package gate: $($unexpectedOverrides -join ', ')"
}

Write-Host "Release policy verified: $($projects.Count) source projects default to non-packable; SDK and MIT metadata are pinned."
