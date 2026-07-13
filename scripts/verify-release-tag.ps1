param(
    [Parameter(Mandatory)]
    [string]$TagName
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$topology = Get-Content -Raw (Join-Path $root 'eng/package-topology.json') | ConvertFrom-Json
$expected = "v$($topology.version)"

if ($TagName -cne $expected) {
    throw "Release tag '$TagName' does not exactly match package version '$expected'."
}

Write-Host "Release tag verified: $TagName."
