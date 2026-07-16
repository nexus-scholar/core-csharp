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
    New-Item (Join-Path $tempRoot 'eng') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'tests/NexusScholar.PackageSmoke') -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $tempRoot 'scripts') -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'verify-release-policy.ps1') (Join-Path $tempRoot 'scripts/verify-release-policy.ps1')

    @'
<Project>
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <VersionPrefix>0.0.1</VersionPrefix>
    <VersionSuffix>alpha.1</VersionSuffix>
  </PropertyGroup>
</Project>
'@ | Set-Content (Join-Path $tempRoot 'Directory.Build.props') -Encoding utf8
    '{"sdk":{"version":"10.0.100","rollForward":"disable","allowPrerelease":false}}' |
        Set-Content (Join-Path $tempRoot 'global.json') -Encoding utf8
    'test license' | Set-Content (Join-Path $tempRoot 'LICENSE') -Encoding utf8
    '{"version":"0.0.1-alpha.1","packages":["Test.Package"],"smokeRoots":["Test.Package"]}' |
        Set-Content (Join-Path $tempRoot 'eng/package-topology.json') -Encoding utf8
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>' |
        Set-Content (Join-Path $tempRoot 'src/Test.Package/Test.Package.csproj') -Encoding utf8
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
        (Get-Content -Raw tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj).Replace('alpha.1', 'alpha.2') |
            Set-Content tests/NexusScholar.PackageSmoke/NexusScholar.PackageSmoke.csproj -Encoding utf8

        & ./scripts/verify-release-policy.ps1
    }
    finally {
        Pop-Location
    }

    Write-Host 'Release policy regressions passed: matching tag and untagged version accepted; reused tag rejected.'
}
finally {
    if (Test-Path -LiteralPath $resolvedTempRoot) {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
