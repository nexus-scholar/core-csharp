using System.Text.Json;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;

namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceScreeningAuthorityPackageManifest(
    string Schema,
    string GenerationId,
    string WorkspaceId,
    long ProjectRevision,
    string SourceAuthorityGenerationId,
    string SourceAuthorityManifestSha256,
    string SourceResultId,
    string SourceResultDigest,
    string SourceSnapshotId,
    string SourceSnapshotRecordDigest,
    string DecisionSetDigest,
    string ProtocolVersionId,
    string ProtocolContentDigest,
    string CriteriaId,
    string CriteriaDigest,
    bool WorkflowGoverned,
    IReadOnlyList<ResearchWorkspaceGenerationArtifact> Artifacts)
{
    public const string CurrentSchema = "nexus.screening-authority-package.manifest.v1";
}

public sealed record VerifiedResearchWorkspaceScreeningAuthorityPackage(
    ResearchWorkspaceScreeningAuthorityPackageManifest Manifest,
    VerifiedDeduplicationAuthorityResultDigest SourceResultAuthority,
    VerifiedDeduplicationResult Deduplication,
    ResearchWorkspaceVerifiedAuthorityChain DeduplicationAuthorityChain,
    VerifiedProtocolVersion Protocol,
    ScreeningCriteria Criteria);

public sealed record ResearchWorkspaceScreeningAuthorityPackageCommit(
    ResearchWorkspaceProject Project,
    VerifiedResearchWorkspaceScreeningAuthorityPackage Package,
    bool AlreadyApplied);

public sealed record ResearchWorkspaceScreeningAuthorityReadiness(
    ResearchWorkspaceOperationStatus Status,
    string Category,
    int ExitCode,
    string Message,
    string? WorkspaceId = null,
    long? ProjectRevision = null,
    string? GenerationId = null,
    string? ProtocolVersionId = null,
    string? ProtocolContentDigest = null,
    string? CriteriaId = null,
    string? CriteriaDigest = null,
    string? SourceSnapshotId = null,
    string? SourceSnapshotDigest = null,
    bool WorkflowGoverned = false)
{
    public bool Ready => Status == ResearchWorkspaceOperationStatus.Succeeded;
}

public static class ResearchWorkspaceScreeningAuthorityPackage
{
    public const string ReadyCategory = "ready";
    public const string UnavailableCategory = "unavailable";
    public const string StaleCategory = "stale";
    public const string InvalidCategory = "invalid";
    public const string RecoveryRequiredCategory = "recovery-required";
    private const string ManifestFileName = "screening-authority-package.manifest.json";
    private const string ProtocolFileName = "protocol-authority.json";
    private const string CriteriaFileName = "criteria.json";

    public static ResearchWorkspaceScreeningAuthorityPackageCommit Commit(
        string workingDirectory,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(criteria);
        var state = LoadAuthorityState(workingDirectory);
        if (protocol.Version.Status != ProtocolStatus.Approved)
        {
            throw new InvalidOperationException("Screening authority requires a currently approved Protocol version.");
        }
        if (!string.Equals(protocol.Version.ProjectId, state.Project.WorkspaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Screening authority Protocol must belong to the current workspace.");
        }
        if (!string.IsNullOrWhiteSpace(criteria.WorkflowBinding))
        {
            throw new InvalidOperationException(
                "Workflow-bound Screening criteria require a verified Workflow authority package.");
        }

        var protocolBytes = ProtocolAuthorityPackageCanonicalCodec.Serialize(protocol);
        var protocolRawDigest = ContentDigest.Sha256(protocolBytes);
        var criteriaBytes = ScreeningCriteriaCanonicalCodec.Serialize(criteria);
        _ = ScreeningCriteriaCanonicalCodec.Rehydrate(criteriaBytes, criteria.ComputeDigest(), protocol);
        var criteriaDigest = criteria.ComputeDigest();
        if (state.Project.CurrentScreeningAuthorityPackageGenerationId is not null)
        {
            try
            {
                var current = VerifyCurrent(state.Location.RootDirectory);
                if (current.Protocol.Version.ContentDigest == protocol.Version.ContentDigest &&
                    current.Criteria.ComputeDigest() == criteriaDigest)
                {
                    return new ResearchWorkspaceScreeningAuthorityPackageCommit(state.Project, current, true);
                }
            }
            catch (ResearchWorkspaceScreeningAuthorityException exception) when (exception.Category == StaleCategory)
            {
                // A stale package is replaced below with a revision-bound successor.
            }
        }

        var identity = ContentDigest.Sha256Utf8(
            $"{state.Project.Revision}|{state.Project.CurrentAuthorityGenerationId}|{state.Project.AuthorityGenerationManifestSha256}|" +
            $"{state.Chain.CurrentSnapshot.RecordDigest}|{protocol.Version.ContentDigest}|{criteriaDigest}");
        var generationId = $"screening-authority-{identity.Value[7..31]}";
        var relativeRoot = ResearchWorkspacePaths.ScreeningAuthorityPackageRoot(generationId);
        var finalRoot = ResearchWorkspacePaths.InProject(state.Location.RootDirectory, relativeRoot);
        var manifestPath = $"{relativeRoot}/{ManifestFileName}";

        var stagingRoot = ResearchWorkspacePaths.InProject(
            state.Location.RootDirectory,
            $"{ResearchWorkspacePaths.GenerationStaging}/{generationId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        var promoted = false;
        var projectWritten = false;
        try
        {
            File.WriteAllBytes(Path.Combine(stagingRoot, ProtocolFileName), protocolBytes);
            File.WriteAllBytes(Path.Combine(stagingRoot, CriteriaFileName), criteriaBytes);
            var artifacts = new[]
            {
                new ResearchWorkspaceGenerationArtifact(
                    "criteria", $"{relativeRoot}/{CriteriaFileName}", ContentDigest.Sha256(criteriaBytes).ToString()),
                new ResearchWorkspaceGenerationArtifact(
                    "protocol-authority", $"{relativeRoot}/{ProtocolFileName}", protocolRawDigest.ToString())
            };
            var placeholder = "sha256:" + new string('0', 64);
            var committed = state.Project.CommitScreeningAuthorityPackageGeneration(generationId, manifestPath, placeholder);
            var manifest = new ResearchWorkspaceScreeningAuthorityPackageManifest(
                ResearchWorkspaceScreeningAuthorityPackageManifest.CurrentSchema,
                generationId,
                state.Project.WorkspaceId,
                committed.Revision,
                state.Project.CurrentAuthorityGenerationId!,
                state.Project.AuthorityGenerationManifestSha256!,
                state.Source.Result.ResultId,
                state.Source.ResultDigest.ToString(),
                state.Chain.CurrentSnapshot.SnapshotId,
                state.Chain.CurrentSnapshot.RecordDigest.ToString(),
                state.Chain.CurrentSnapshot.DecisionSetDigest.ToString(),
                protocol.Version.Id,
                protocol.Version.ContentDigest.ToString(),
                criteria.CriteriaId,
                criteriaDigest.ToString(),
                WorkflowGoverned: false,
                artifacts);
            var manifestBytes = SerializeManifest(manifest);
            committed = committed with
            {
                ScreeningAuthorityPackageManifestSha256 = ContentDigest.Sha256(manifestBytes).ToString()
            };
            File.WriteAllBytes(Path.Combine(stagingRoot, ManifestFileName), manifestBytes);
            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterStaging);

            Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
            using var workspaceLock = AcquireLock(state.Location);
            var currentProject = ResearchWorkspaceStore.ReadProject(state.Location.ProjectFilePath);
            if (currentProject.Revision != state.Project.Revision ||
                currentProject.CurrentAuthorityGenerationId != state.Project.CurrentAuthorityGenerationId ||
                currentProject.AuthorityGenerationManifestSha256 != state.Project.AuthorityGenerationManifestSha256 ||
                currentProject.CurrentScreeningAuthorityPackageGenerationId != state.Project.CurrentScreeningAuthorityPackageGenerationId ||
                currentProject.ScreeningAuthorityPackageManifestSha256 != state.Project.ScreeningAuthorityPackageManifestSha256)
            {
                throw new ResearchWorkspaceConcurrencyException(state.Project.Revision, currentProject.Revision);
            }

            if (Directory.Exists(finalRoot))
            {
                if (!DirectoriesMatch(stagingRoot, finalRoot))
                {
                    throw new ResearchWorkspaceConcurrencyException(
                        "Screening authority package identity collides with different bytes.",
                        new InvalidOperationException());
                }

                Directory.Delete(stagingRoot, true);
            }
            else
            {
                Directory.Move(stagingRoot, finalRoot);
                promoted = true;
            }

            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterPromotion);
            ResearchWorkspaceStore.WriteProject(state.Location, committed);
            projectWritten = true;
            var verified = VerifyCurrent(state.Location.RootDirectory);
            return new ResearchWorkspaceScreeningAuthorityPackageCommit(committed, verified, false);
        }
        catch
        {
            if (promoted && !projectWritten && Directory.Exists(finalRoot))
            {
                var quarantine = ResearchWorkspacePaths.InProject(
                    state.Location.RootDirectory,
                    $"{ResearchWorkspacePaths.GenerationQuarantine}/{generationId}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
                Directory.Move(finalRoot, quarantine);
            }

            throw;
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, true);
            }
        }
    }

    public static VerifiedResearchWorkspaceScreeningAuthorityPackage VerifyCurrent(string workingDirectory)
    {
        var state = LoadAuthorityState(workingDirectory);
        var project = state.Project;
        if (project.CurrentScreeningAuthorityPackageGenerationId is null ||
            project.ScreeningAuthorityPackageManifestPath is null ||
            !ContentDigest.TryParse(project.ScreeningAuthorityPackageManifestSha256, out var expectedManifestDigest))
        {
            throw new ResearchWorkspaceMissingInputException("No Screening authority package is available.");
        }

        var manifestPath = Resolve(state.Location, project.ScreeningAuthorityPackageManifestPath);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        if (ContentDigest.Sha256(manifestBytes) != expectedManifestDigest)
        {
            throw new InvalidOperationException("Screening authority package manifest digest does not match its pointer.");
        }

        var manifest = ParseManifest(manifestBytes);
        if (manifest.GenerationId != project.CurrentScreeningAuthorityPackageGenerationId ||
            manifest.WorkspaceId != project.WorkspaceId ||
            manifest.ProjectRevision > project.Revision ||
            manifest.SourceAuthorityGenerationId != project.CurrentAuthorityGenerationId ||
            manifest.SourceAuthorityManifestSha256 != project.AuthorityGenerationManifestSha256 ||
            manifest.SourceResultId != state.Source.Result.ResultId ||
            manifest.SourceResultDigest != state.Source.ResultDigest.ToString() ||
            manifest.SourceSnapshotId != state.Chain.CurrentSnapshot.SnapshotId ||
            manifest.SourceSnapshotRecordDigest != state.Chain.CurrentSnapshot.RecordDigest.ToString() ||
            manifest.DecisionSetDigest != state.Chain.CurrentSnapshot.DecisionSetDigest.ToString())
        {
            throw new ResearchWorkspaceScreeningAuthorityException(
                StaleCategory,
                "Screening authority package is stale for the current workspace authority.");
        }

        if (manifest.WorkflowGoverned)
        {
            throw new InvalidOperationException("Workflow-governed Screening requires a verified Workflow authority artifact.");
        }

        var artifacts = ReadArtifacts(state.Location, manifest.Artifacts);
        var protocol = ProtocolAuthorityPackageCanonicalCodec.Rehydrate(
            artifacts["protocol-authority"].Bytes,
            artifacts["protocol-authority"].RawDigest);
        var criteria = ScreeningCriteriaCanonicalCodec.Rehydrate(
            artifacts["criteria"].Bytes,
            ContentDigest.Parse(manifest.CriteriaDigest),
            protocol);
        if (!string.Equals(protocol.Version.ProjectId, project.WorkspaceId, StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(criteria.WorkflowBinding))
        {
            throw new InvalidOperationException(
                "Screening authority package Protocol, Workflow, or workspace binding is invalid.");
        }
        if (manifest.ProtocolVersionId != protocol.Version.Id ||
            manifest.ProtocolContentDigest != protocol.Version.ContentDigest.ToString() ||
            manifest.CriteriaId != criteria.CriteriaId ||
            manifest.CriteriaDigest != criteria.ComputeDigest().ToString())
        {
            throw new InvalidOperationException("Screening authority package manifest authority bindings do not reproduce.");
        }

        var deduplication = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(state.Source.Result));
        return new VerifiedResearchWorkspaceScreeningAuthorityPackage(
            manifest, state.Source, deduplication, state.Chain, protocol, criteria);
    }

    public static ResearchWorkspaceScreeningAuthorityReadiness Inspect(string workingDirectory)
    {
        try
        {
            var package = VerifyCurrent(workingDirectory);
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Succeeded,
                ReadyCategory,
                ResearchWorkspaceExitCodes.Success,
                "Verified title/abstract Screening authority is ready.",
                package.Manifest.WorkspaceId,
                package.Manifest.ProjectRevision,
                package.Manifest.GenerationId,
                package.Manifest.ProtocolVersionId,
                package.Manifest.ProtocolContentDigest,
                package.Manifest.CriteriaId,
                package.Manifest.CriteriaDigest,
                package.Manifest.SourceSnapshotId,
                package.Manifest.SourceSnapshotRecordDigest,
                package.Manifest.WorkflowGoverned);
        }
        catch (ResearchWorkspaceMissingInputException exception)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Failed,
                UnavailableCategory,
                ResearchWorkspaceExitCodes.MissingProjectOrInput,
                exception.Message);
        }
        catch (ResearchWorkspaceScreeningAuthorityException exception) when (exception.Category == StaleCategory)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Stale,
                StaleCategory,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (ProtocolRuleException exception)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Failed,
                InvalidCategory,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                $"{exception.Category}: {exception.Message}");
        }
        catch (ScreeningRuleException exception)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Failed,
                InvalidCategory,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                $"{exception.Category}: {exception.Message}");
        }
        catch (JsonException)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Failed,
                InvalidCategory,
                ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat,
                "Screening authority package contains invalid canonical material.");
        }
        catch (ResearchWorkspaceConcurrencyException exception)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.RecoveryRequired,
                RecoveryRequiredCategory,
                ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                exception.Message);
        }
        catch (InvalidOperationException)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.Failed,
                InvalidCategory,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                "Screening authority package is invalid or does not reproduce.");
        }
        catch (Exception)
        {
            return new ResearchWorkspaceScreeningAuthorityReadiness(
                ResearchWorkspaceOperationStatus.RecoveryRequired,
                RecoveryRequiredCategory,
                ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                "Screening authority could not be reconstructed from the local workspace.");
        }
    }

    private static WorkspaceAuthorityState LoadAuthorityState(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var location = ResearchWorkspaceStore.FindFrom(Path.GetFullPath(workingDirectory))
            ?? throw new ResearchWorkspaceMissingInputException("No Nexus research workspace was found in the selected folder.");
        var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        if (project.CurrentAuthorityGenerationId is null || project.AuthorityGenerationManifestSha256 is null)
        {
            throw new ResearchWorkspaceMissingInputException("An initialized Deduplication authority generation is required.");
        }

        var relativePath = project.Outputs.GetValueOrDefault("deduplicationResult")
            ?? ResearchWorkspacePaths.CurrentDeduplicationResult;
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var resultPath) ||
            !File.Exists(resultPath))
        {
            throw new ResearchWorkspaceMissingInputException("The current Deduplication result is missing or outside the workspace.");
        }

        var result = JsonSerializer.Deserialize<DeduplicationResult>(
            File.ReadAllBytes(resultPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("The current Deduplication result is empty.");
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(location, project, source);
        return new WorkspaceAuthorityState(location, project, source, chain);
    }

    private static byte[] SerializeManifest(ResearchWorkspaceScreeningAuthorityPackageManifest value)
    {
        var artifacts = value.Artifacts
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                .Add("name", item.Name)
                .Add("relative_path", item.RelativePath)
                .Add("sha256", item.Sha256))
            .ToArray();
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(new CanonicalJsonObject()
            .Add("artifacts", CanonicalJsonValue.Array(artifacts))
            .Add("criteria_digest", value.CriteriaDigest)
            .Add("criteria_id", value.CriteriaId)
            .Add("decision_set_digest", value.DecisionSetDigest)
            .Add("generation_id", value.GenerationId)
            .Add("project_revision", value.ProjectRevision)
            .Add("protocol_content_digest", value.ProtocolContentDigest)
            .Add("protocol_version_id", value.ProtocolVersionId)
            .Add("schema", value.Schema)
            .Add("source_authority_generation_id", value.SourceAuthorityGenerationId)
            .Add("source_authority_manifest_sha256", value.SourceAuthorityManifestSha256)
            .Add("source_result_digest", value.SourceResultDigest)
            .Add("source_result_id", value.SourceResultId)
            .Add("source_snapshot_id", value.SourceSnapshotId)
            .Add("source_snapshot_record_digest", value.SourceSnapshotRecordDigest)
            .Add("workflow_governed", value.WorkflowGoverned)
            .Add("workspace_id", value.WorkspaceId));
    }

    private static ResearchWorkspaceScreeningAuthorityPackageManifest ParseManifest(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
        if (!bytes.SequenceEqual(canonical))
        {
            throw new InvalidOperationException("Screening authority package manifest is not canonical.");
        }

        var root = document.RootElement;
        var properties = root.EnumerateObject().Select(item => item.Name).ToArray();
        var expected = new[]
        {
            "artifacts", "criteria_digest", "criteria_id", "decision_set_digest", "generation_id",
            "project_revision", "protocol_content_digest", "protocol_version_id", "schema",
            "source_authority_generation_id", "source_authority_manifest_sha256", "source_result_digest",
            "source_result_id", "source_snapshot_id", "source_snapshot_record_digest", "workflow_governed",
            "workspace_id"
        };
        if (!properties.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Screening authority package manifest fields are invalid.");
        }

        var artifacts = root.GetProperty("artifacts").EnumerateArray().Select(item =>
            new ResearchWorkspaceGenerationArtifact(
                item.GetProperty("name").GetString()!,
                item.GetProperty("relative_path").GetString()!,
                item.GetProperty("sha256").GetString()!)).ToArray();
        var manifest = new ResearchWorkspaceScreeningAuthorityPackageManifest(
            root.GetProperty("schema").GetString()!,
            root.GetProperty("generation_id").GetString()!,
            root.GetProperty("workspace_id").GetString()!,
            root.GetProperty("project_revision").GetInt64(),
            root.GetProperty("source_authority_generation_id").GetString()!,
            root.GetProperty("source_authority_manifest_sha256").GetString()!,
            root.GetProperty("source_result_id").GetString()!,
            root.GetProperty("source_result_digest").GetString()!,
            root.GetProperty("source_snapshot_id").GetString()!,
            root.GetProperty("source_snapshot_record_digest").GetString()!,
            root.GetProperty("decision_set_digest").GetString()!,
            root.GetProperty("protocol_version_id").GetString()!,
            root.GetProperty("protocol_content_digest").GetString()!,
            root.GetProperty("criteria_id").GetString()!,
            root.GetProperty("criteria_digest").GetString()!,
            root.GetProperty("workflow_governed").GetBoolean(),
            artifacts);
        if (manifest.Schema != ResearchWorkspaceScreeningAuthorityPackageManifest.CurrentSchema ||
            manifest.ProjectRevision <= 0 ||
            manifest.Artifacts.Count != 2 ||
            manifest.Artifacts.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != 2 ||
            !manifest.Artifacts.Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal)
                .SequenceEqual(new[] { "criteria", "protocol-authority" }, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Screening authority package manifest content is invalid.");
        }

        return manifest;
    }

    private static IReadOnlyDictionary<string, ArtifactBytes> ReadArtifacts(
        ResearchWorkspaceLocation location,
        IReadOnlyList<ResearchWorkspaceGenerationArtifact> artifacts)
    {
        var result = new Dictionary<string, ArtifactBytes>(StringComparer.Ordinal);
        foreach (var artifact in artifacts)
        {
            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(
                    location.RootDirectory, artifact.RelativePath, out var path) ||
                !File.Exists(path) ||
                !ContentDigest.TryParse(artifact.Sha256, out var expectedDigest))
            {
                throw new InvalidOperationException("Screening authority package artifact is missing or invalid.");
            }

            var bytes = File.ReadAllBytes(path);
            if (ContentDigest.Sha256(bytes) != expectedDigest || !result.TryAdd(artifact.Name, new ArtifactBytes(bytes, expectedDigest)))
            {
                throw new InvalidOperationException("Screening authority package artifact digest or identity is invalid.");
            }
        }

        return result;
    }

    private static string Resolve(ResearchWorkspaceLocation location, string relativePath)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var path))
        {
            throw new InvalidOperationException("Screening authority package path is outside the workspace.");
        }

        return path;
    }

    private static FileStream AcquireLock(ResearchWorkspaceLocation location) => new(
        Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName),
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None);

    private static bool DirectoriesMatch(string left, string right)
    {
        var leftFiles = Directory.GetFiles(left).Select(Path.GetFileName).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var rightFiles = Directory.GetFiles(right).Select(Path.GetFileName).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        return leftFiles.SequenceEqual(rightFiles, StringComparer.Ordinal) &&
            leftFiles.All(file => File.ReadAllBytes(Path.Combine(left, file!))
                .SequenceEqual(File.ReadAllBytes(Path.Combine(right, file!))));
    }

    private sealed record WorkspaceAuthorityState(
        ResearchWorkspaceLocation Location,
        ResearchWorkspaceProject Project,
        VerifiedDeduplicationAuthorityResultDigest Source,
        ResearchWorkspaceVerifiedAuthorityChain Chain);

    private sealed record ArtifactBytes(byte[] Bytes, ContentDigest RawDigest);
}

public sealed class ResearchWorkspaceScreeningAuthorityException : InvalidOperationException
{
    public ResearchWorkspaceScreeningAuthorityException(string category, string message) : base(message)
    {
        Category = category;
    }

    public string Category { get; }
}
