param(
    [string]$Repository = 'nexus-scholar/core-csharp'
)

$ErrorActionPreference = 'Stop'

function Get-GitHubJson([string]$Path) {
    $endpoint = "repos/$Repository"
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        $endpoint = "$endpoint/$Path"
    }
    $output = gh api $endpoint
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed for '$Path'."
    }
    return $output | ConvertFrom-Json
}

$branch = Get-GitHubJson 'branches/main'
if (-not $branch.protected) { throw 'main is not protected.' }

$protection = Get-GitHubJson 'branches/main/protection'
$expectedChecks = @('analyze', 'review', 'verify (ubuntu-latest)', 'verify (windows-latest)')
$actualChecks = @($protection.required_status_checks.contexts | Sort-Object)
if (-not $protection.required_status_checks.strict -or
    (Compare-Object ($expectedChecks | Sort-Object) $actualChecks)) {
    throw 'main does not enforce the approved strict status-check set.'
}
if (-not $protection.enforce_admins.enabled -or
    -not $protection.required_conversation_resolution.enabled -or
    $protection.allow_force_pushes.enabled -or
    $protection.allow_deletions.enabled) {
    throw 'main protection does not enforce the approved administration and history controls.'
}

$privateReporting = Get-GitHubJson 'private-vulnerability-reporting'
if (-not $privateReporting.enabled) { throw 'Private vulnerability reporting is disabled.' }

$repositoryState = Get-GitHubJson ''
if ($repositoryState.security_and_analysis.dependabot_security_updates.status -ne 'enabled' -or
    $repositoryState.security_and_analysis.secret_scanning.status -ne 'enabled' -or
    $repositoryState.security_and_analysis.secret_scanning_push_protection.status -ne 'enabled') {
    throw 'Dependency or secret-scanning controls are disabled.'
}

$environment = Get-GitHubJson 'environments/release'
if (-not $environment.deployment_branch_policy.custom_branch_policies -or
    $environment.deployment_branch_policy.protected_branches) {
    throw 'The release environment does not require a custom deployment policy.'
}
$policies = Get-GitHubJson 'environments/release/deployment-branch-policies'
$tagPolicies = @($policies.branch_policies | Where-Object { $_.type -eq 'tag' -and $_.name -eq 'v*' })
if ($tagPolicies.Count -ne 1) { throw 'The release environment must allow exactly the v* tag policy.' }

$pages = Get-GitHubJson 'pages'
if ($pages.build_type -ne 'workflow' -or $pages.status -ne 'built' -or -not $pages.https_enforced) {
    throw 'GitHub Pages is not built through the HTTPS workflow pipeline.'
}

Write-Host "GitHub governance verified for ${Repository}: protected main, private reporting, security analysis, tag-only release environment, and workflow Pages."
