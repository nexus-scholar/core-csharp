# Gate 1 Evidence

Date: 2026-06-26

## Exact Commit

- `ee46eb48765ccf7975e6895f66d97a7cbafa12b4`

## Repository State At Start

- The working tree was not clean before this pass.
- Pre-existing Gate 0 document changes were already present under `docs/gates/`, `docs/discovery/`, and `docs/port/`.
- This Gate 1 pass added only:
  - `.github/workflows/gate-01.yml`
  - `tests/NexusScholar.Architecture.Tests/RepositoryPolicyTests.cs`
  - an expanded `tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs`
  - `docs/gates/GATE-01.md`
  - `docs/gates/GATE-01-EVIDENCE.md`

## Commands And Outputs

### 1. Restore

Command:

```powershell
dotnet restore NexusScholar.Core.slnx
```

Output:

```text
Determining projects to restore...
All projects are up-to-date for restore.
```

Result:

- Pass

### 2. Release Build

Command:

```powershell
dotnet build NexusScholar.Core.slnx -c Release --no-restore
```

Output summary:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Result:

- Pass

### 3. All Test Projects

Command:

```powershell
dotnet test NexusScholar.Core.slnx -c Release --no-build
```

Output summary:

```text
NexusScholar.Conformance.Tests: Passed 1
NexusScholar.Core.Tests: Passed 6
NexusScholar.Architecture.Tests: Passed 3
Failed: 0
```

Result:

- Pass

### 4. Formatting

Command:

```powershell
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Output summary:

```text
Exit code 0 with no formatting violations.
```

Result:

- Pass

### 5. Repository Verify Script

Command:

```powershell
pwsh -NoProfile -File .\scripts\verify.ps1
```

Output summary:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
Architecture tests: Passed 3
Core tests: Passed 6
Conformance tests: Passed 1
```

Result:

- Pass

### 6. Linux Shell Path In This Session

Command:

```powershell
wsl.exe -e bash -lc "cd '/mnt/c/Users/mouadh/Documents/AI in research/core-csharp' && ./scripts/verify.sh"
```

Output:

```text
./scripts/verify.sh: line 6: dotnet: command not found
```

Result:

- Fail in local WSL only because the Ubuntu environment on this machine does not have `dotnet` installed.
- This is not a repository defect.
- It is evidence that hosted Linux CI must provide its own .NET setup step.

## CI Run Evidence

### GitHub Actions workflow added

- `.github/workflows/gate-01.yml`

### CI matrix

- `ubuntu-latest`
- `windows-latest`

### CI steps

1. checkout
2. setup-dotnet from `global.json`
3. `dotnet restore NexusScholar.Core.slnx`
4. `dotnet build NexusScholar.Core.slnx -c Release --no-restore`
5. `dotnet test NexusScholar.Core.slnx -c Release --no-build`
6. `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`

### CI evidence boundary

- Hosted GitHub Actions was not executed from this local session.
- Windows CI and Linux CI are configured, but no remote run id or hosted run log exists yet in this evidence file.
- Local Windows verification is evidenced above.
- Local WSL Linux verification could not complete because the local Ubuntu image lacks `dotnet`.

## Architecture And Offline-Execution Evidence

### Forbidden domain dependency proof

- `Directory.Build.props` keeps warnings-as-errors and deterministic builds.
- `global.json` pins .NET 10.
- `tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs` now blocks referenced assemblies beginning with:
  - `Microsoft.EntityFrameworkCore`
  - `Microsoft.AspNetCore`
  - `Avalonia`
  - `Amazon.`
  - `AWSSDK.`
  - `Azure.`
  - `Google.`
  - `OpenAI`
  - `Anthropic`
  - `Microsoft.SemanticKernel`
  - `System.Net.Http`

### Offline and live-call proof

- `tests/NexusScholar.Architecture.Tests/RepositoryPolicyTests.cs` now fails if repository `.csproj` files reference forbidden provider/framework packages.
- The same test file fails if `src/` or `tests/` introduces direct live-call or provider SDK symbols such as:
  - `HttpClient`
  - `HttpRequestMessage`
  - `WebRequest`
  - `Socket`
  - `TcpClient`
  - `UdpClient`
  - `OpenAI`
  - `Anthropic`
  - `SemanticKernel`
  - `Azure.AI`
  - `Azure.Storage`
  - `Google.Cloud`
  - `Google.Apis`
  - `Amazon.S3`
  - `AWSSDK`

### What this evidence means

- Gate 1 now has automated repository guards for forbidden dependency families and obvious live-call primitives.
- This is repository-quality enforcement only.
- It is not protocol compatibility evidence.

## Exit Checklist

- `.NET 10 restore succeeds`: Yes
- `Release build succeeds with zero warnings`: Yes
- `All test projects pass`: Yes
- `Formatting verification passes`: Yes
- `Windows CI configured`: Yes
- `Linux CI configured`: Yes
- `Hosted Windows CI run evidenced`: No
- `Hosted Linux CI run evidenced`: No
- `No live provider or LLM calls occur in tests`: Enforced by repository policy tests and current passing test suite
- `No forbidden domain dependencies`: Enforced by architecture tests and current passing test suite

## Remaining Risks

- No hosted GitHub Actions run evidence exists yet for the new workflow.
- The local WSL Ubuntu environment lacks `dotnet`, so Linux verification in this session stopped before repository execution.
- Repository policy tests use static pattern guards; they are intentionally conservative but not a full semantic sandbox.
- Conformance remains scaffold-level only because `OPEN-CONFLICTS.md` still blocks blueprint and PHP compatibility claims.
- Pre-existing Gate 0 documentation changes remain in the working tree and are unrelated to Gate 1 verification itself.

## Explicit Claims Not Made

- No blueprint contracts were adopted.
- No blueprint conformance is claimed.
- No PHP compatibility is claimed.
- No PHP compatibility fixtures were created.
- No Gate 2 domain behavior was implemented.
- No Gate 2+ conflicts were resolved implicitly.
- No hosted GitHub Actions run is claimed from this session.

## Gate 1 Verdict

Local repository-quality verification passes on Windows, and hosted CI is now configured for Windows and Linux. Gate 1 is ready for the first hosted CI run, but this evidence set does not claim hosted CI execution has already occurred.
