$ErrorActionPreference = "Stop"

$filter = "Name~mutation|Name~rehydrat|Name~forged|Name~tamper|Name~non_finite|Name~nonfinite"

dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --filter $filter
if ($LASTEXITCODE -ne 0) {
    throw "Core scientific-invariant mutation matrix failed with exit code $LASTEXITCODE."
}

dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --filter $filter
if ($LASTEXITCODE -ne 0) {
    throw "Conformance mutation matrix failed with exit code $LASTEXITCODE."
}
