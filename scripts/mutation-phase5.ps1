$ErrorActionPreference = "Stop"
$minimumSupportedMajorVersion = 7
if ($PSVersionTable.PSVersion.Major -lt $minimumSupportedMajorVersion) {
    throw "scripts/mutation-phase5.ps1 requires PowerShell 7+ (pwsh). Use `pwsh` instead of Windows PowerShell."
}

$root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/resolve-dotnet.ps1"
$dotnet = Use-PinnedDotNet $root
$manifestPath = Join-Path $root "eng/scientific-invariant-mutation-tests.txt"
$projects = [ordered]@{
    "core" = "tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj"
    "conformance" = "tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj"
    "workspace" = "tests/NexusScholar.ResearchWorkspace.Tests/NexusScholar.ResearchWorkspace.Tests.csproj"
    "fulltext" = "tests/NexusScholar.FullText.Retrieval.Tests/NexusScholar.FullText.Retrieval.Tests.csproj"
    "cache" = "tests/NexusScholar.Search.Providers.Cache.Tests/NexusScholar.Search.Providers.Cache.Tests.csproj"
    "crossref" = "tests/NexusScholar.Search.Providers.Crossref.Tests/NexusScholar.Search.Providers.Crossref.Tests.csproj"
    "openalex" = "tests/NexusScholar.Search.Providers.OpenAlex.Tests/NexusScholar.Search.Providers.OpenAlex.Tests.csproj"
    "semantic-scholar" = "tests/NexusScholar.Search.Providers.SemanticScholar.Tests/NexusScholar.Search.Providers.SemanticScholar.Tests.csproj"
    "live" = "tests/NexusScholar.Search.Providers.Live.Tests/NexusScholar.Search.Providers.Live.Tests.csproj"
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Scientific-invariant mutation manifest is missing: $manifestPath"
}

$matrix = @(
    Get-Content -LiteralPath $manifestPath |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith("#") } |
        ForEach-Object {
            $parts = $_.Split("|", 3)
            if ($parts.Count -lt 2 -or
                [string]::IsNullOrWhiteSpace($parts[0]) -or
                [string]::IsNullOrWhiteSpace($parts[1])) {
                throw "Invalid scientific-invariant manifest entry: $_"
            }

            $expectedOutcome = if ($parts.Count -eq 3 -and
                -not [string]::IsNullOrWhiteSpace($parts[2])) {
                $parts[2].Trim()
            }
            else {
                "passed"
            }
            if ($expectedOutcome -notin @("passed", "windows-skip")) {
                throw "Unknown scientific-invariant expected outcome '$expectedOutcome' in entry: $_"
            }

            [pscustomobject]@{
                Project = $parts[0].Trim()
                TestName = $parts[1].Trim()
                ExpectedOutcome = $expectedOutcome
            }
        }
)

if ($matrix.Count -eq 0) {
    throw "Scientific-invariant mutation manifest is empty."
}

$unknownProjects = @($matrix |
    Where-Object { -not $projects.Contains($_.Project) } |
    Select-Object -ExpandProperty Project -Unique)
if ($unknownProjects.Count -gt 0) {
    throw "Scientific-invariant manifest contains unknown project aliases: $($unknownProjects -join ', ')"
}

$duplicates = @($matrix |
    Group-Object { "$($_.Project)|$($_.TestName)" } |
    Where-Object Count -gt 1)
if ($duplicates.Count -gt 0) {
    throw "Scientific-invariant manifest contains duplicate entries: $($duplicates.Name -join ', ')"
}

$resultRoot = Join-Path ([IO.Path]::GetTempPath()) "nexus-mutation-phase5-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $resultRoot | Out-Null

try {
    foreach ($projectAlias in @($matrix.Project | Sort-Object -Unique)) {
        $projectPath = $projects[$projectAlias]
        $trxName = "$projectAlias.trx"
        & $dotnet test $projectPath -c Release --no-build `
            --logger "trx;LogFileName=$trxName" `
            --results-directory $resultRoot
        if ($LASTEXITCODE -ne 0) {
            throw "$projectAlias scientific-invariant project suite failed with exit code $LASTEXITCODE."
        }

        $trxPath = Join-Path $resultRoot $trxName
        if (-not (Test-Path -LiteralPath $trxPath)) {
            throw "$projectAlias scientific-invariant result file is missing."
        }

        [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
        $results = @($trx.TestRun.Results.UnitTestResult)
        foreach ($expected in @($matrix | Where-Object Project -eq $projectAlias)) {
            $matches = @($results | Where-Object {
                [string]::Equals(
                    [string]$_.testName,
                    $expected.TestName,
                    [StringComparison]::Ordinal)
            })
            if ($matches.Count -eq 0) {
                throw "$projectAlias required scientific-invariant test was not executed: $($expected.TestName)"
            }
            if ($matches.Count -gt 1) {
                throw "$projectAlias scientific-invariant test name is ambiguous: $($expected.TestName)"
            }

            $outcome = [string]$matches[0].outcome
            if ([string]::Equals($outcome, "Passed", [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $allowedWindowsSkip = $expected.ExpectedOutcome -eq "windows-skip" -and
                [OperatingSystem]::IsWindows() -and
                $outcome -in @("NotExecuted", "Skipped")
            if (-not $allowedWindowsSkip) {
                throw "$projectAlias scientific-invariant test '$($expected.TestName)' completed with outcome '$outcome'."
            }
        }
    }

    Write-Host "Scientific-invariant mutation manifest verified: $($matrix.Count) explicit test cases across $(@($matrix.Project | Sort-Object -Unique).Count) projects."
}
finally {
    if (Test-Path -LiteralPath $resultRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        $resolvedResultRoot = [IO.Path]::GetFullPath($resultRoot)
        $tempPrefix = $tempRoot.TrimEnd(
            [IO.Path]::DirectorySeparatorChar,
            [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedResultRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Split-Path -Leaf $resolvedResultRoot).StartsWith(
                "nexus-mutation-phase5-",
                [StringComparison]::Ordinal)) {
            throw "Refusing to remove unexpected mutation result directory: $resolvedResultRoot"
        }

        Remove-Item -LiteralPath $resolvedResultRoot -Recurse -Force
    }
}
