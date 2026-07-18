using System.Text.Json;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Provenance;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceAuthorityChainVerifier
{
    public static ResearchWorkspaceVerifiedAuthorityChain VerifyCurrent(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        VerifiedDeduplicationAuthorityResultDigest sourceResult)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceResult);
        if (project.CurrentAuthorityGenerationId is null || project.AuthorityGenerationManifestPath is null ||
            !ContentDigest.TryParse(project.AuthorityGenerationManifestSha256, out var currentDigest))
        {
            throw new InvalidOperationException("A complete current authority pointer is required.");
        }

        var manifestPath = Resolve(location, project.AuthorityGenerationManifestPath);
        var currentManifestBytes = File.ReadAllBytes(manifestPath);
        var analysisManifestPath = Resolve(location, project.GenerationManifestPath!);
        var analysisManifestDigest = ContentDigest.Sha256(File.ReadAllBytes(analysisManifestPath));
        using (var currentManifest = JsonDocument.Parse(currentManifestBytes))
        {
            Require(currentManifest.RootElement, "source_analysis_generation_id", project.CurrentGenerationId!);
            Require(currentManifest.RootElement, "source_analysis_manifest_sha256", analysisManifestDigest.ToString());
        }
        var loaded = Load(location, manifestPath, currentDigest, project.WorkspaceId, project.CurrentGenerationId!,
            analysisManifestDigest, sourceResult, new HashSet<string>(StringComparer.Ordinal));
        if (!string.Equals(loaded.GenerationId, project.CurrentAuthorityGenerationId, StringComparison.Ordinal) ||
            loaded.ProjectRevision > project.Revision)
        {
            throw new InvalidOperationException("Current authority generation does not match the project pointer.");
        }

        return loaded;
    }

    private static ResearchWorkspaceVerifiedAuthorityChain Load(
        ResearchWorkspaceLocation location,
        string manifestPath,
        ContentDigest expectedRawDigest,
        string workspaceId,
        string sourceAnalysisGenerationId,
        ContentDigest sourceAnalysisManifestDigest,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        HashSet<string> visited)
    {
        var bytes = File.ReadAllBytes(manifestPath);
        if (ContentDigest.Sha256(bytes) != expectedRawDigest)
        {
            throw new InvalidOperationException("Authority manifest raw digest does not match its pointer.");
        }
        using var document = JsonDocument.Parse(bytes);
        var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
        if (!bytes.SequenceEqual(canonical)) throw new InvalidOperationException("Authority manifest is not canonical.");
        var schema = document.RootElement.GetProperty("schema").GetString();
        var generationId = document.RootElement.GetProperty("authority_generation_id").GetString()!;
        if (!visited.Add(generationId)) throw new InvalidOperationException("Authority generation chain is cyclic.");

        return schema switch
        {
            ResearchWorkspaceAuthorityGenerationManifest.CurrentSchema => LoadBaseline(
                location, document.RootElement, generationId, workspaceId, sourceAnalysisGenerationId,
                sourceAnalysisManifestDigest, sourceResult),
            ResearchWorkspaceSuccessorAuthorityGenerationManifest.CurrentSchema => LoadSuccessor(
                location, bytes, workspaceId, sourceAnalysisGenerationId, sourceAnalysisManifestDigest, sourceResult, visited),
            _ => throw new InvalidOperationException("Authority manifest schema is unsupported.")
        };
    }

    private static ResearchWorkspaceVerifiedAuthorityChain LoadBaseline(
        ResearchWorkspaceLocation location,
        JsonElement root,
        string generationId,
        string workspaceId,
        string sourceAnalysisGenerationId,
        ContentDigest sourceAnalysisManifestDigest,
        VerifiedDeduplicationAuthorityResultDigest sourceResult)
    {
        Require(root, "workspace_id", workspaceId);
        Require(root, "source_analysis_generation_id", sourceAnalysisGenerationId);
        Require(root, "source_analysis_manifest_sha256", sourceAnalysisManifestDigest.ToString());
        Require(root, "source_result_id", sourceResult.Result.ResultId);
        Require(root, "source_result_digest", sourceResult.ResultDigest.ToString());
        if (root.GetProperty("predecessor_authority_generation_id").ValueKind != JsonValueKind.Null ||
            root.GetProperty("predecessor_authority_generation_manifest_sha256").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException("Baseline authority generation cannot have a predecessor.");
        }

        var artifacts = ReadArtifacts(location, root.GetProperty("artifacts"),
            new[] { "authority-policy", "baseline-snapshot", "snapshot-publication-event" });
        var policy = ResearchWorkspaceAuthorityArtifacts.VerifyPolicyCanonicalRecord(artifacts["authority-policy"]);
        var snapshot = ResearchWorkspaceAuthorityArtifacts.VerifySnapshotCanonicalRecord(artifacts["baseline-snapshot"], sourceResult, policy);
        var publication = ResearchWorkspaceAuthorityArtifacts.VerifyResearchEventCanonicalRecord(artifacts["snapshot-publication-event"]);
        Require(root, "authority_policy_id", policy.PolicyId);
        Require(root, "authority_policy_digest", policy.PolicyDigest.ToString());
        Require(root, "decision_set_digest", snapshot.DecisionSetDigest.ToString());
        if (!HasBaselinePublicationBinding(publication, sourceResult, policy, snapshot,
            sourceAnalysisGenerationId, sourceAnalysisManifestDigest))
            throw new InvalidOperationException("Baseline publication provenance binding is invalid.");
        return new ResearchWorkspaceVerifiedAuthorityChain(
            generationId, root.GetProperty("project_revision").GetInt64(), policy, snapshot, publication,
            Array.Empty<VerifiedDeduplicationAuthorityDecision>(), Array.Empty<VerifiedDeduplicationAuthorityDecision>(),
            new[] { snapshot }, Array.Empty<ResearchWorkspaceVerifiedAuthorityTransition>());
    }

    private static ResearchWorkspaceVerifiedAuthorityChain LoadSuccessor(
        ResearchWorkspaceLocation location,
        byte[] manifestBytes,
        string workspaceId,
        string sourceAnalysisGenerationId,
        ContentDigest sourceAnalysisManifestDigest,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        HashSet<string> visited)
    {
        var manifest = ResearchWorkspaceSuccessorAuthorityManifestCodec.ParseCanonical(manifestBytes);
        if (!string.Equals(manifest.WorkspaceId, workspaceId, StringComparison.Ordinal) ||
            !string.Equals(manifest.SourceAnalysisGenerationId, sourceAnalysisGenerationId, StringComparison.Ordinal) ||
            !string.Equals(manifest.SourceAnalysisManifestSha256, sourceAnalysisManifestDigest.ToString(), StringComparison.Ordinal) ||
            !string.Equals(manifest.SourceResultId, sourceResult.Result.ResultId, StringComparison.Ordinal) ||
            !string.Equals(manifest.SourceResultDigest, sourceResult.ResultDigest.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Successor authority manifest source binding is stale.");
        }

        var predecessorPath = Resolve(location,
            $"{ResearchWorkspacePaths.AuthorityGenerationRoot(manifest.PredecessorAuthorityGenerationId)}/authority-generation.manifest.json");
        var predecessor = Load(location, predecessorPath, ContentDigest.Parse(manifest.PredecessorAuthorityGenerationManifestSha256),
            workspaceId, sourceAnalysisGenerationId, sourceAnalysisManifestDigest, sourceResult, visited);
        if (!string.Equals(predecessor.GenerationId, manifest.PredecessorAuthorityGenerationId, StringComparison.Ordinal) ||
            manifest.ProjectRevision <= predecessor.ProjectRevision)
            throw new InvalidOperationException("Successor authority predecessor identity or revision is invalid.");
        var generationRoot = ResearchWorkspacePaths.InProject(location.RootDirectory,
            ResearchWorkspacePaths.AuthorityGenerationRoot(manifest.AuthorityGenerationId));
        var artifactBytes = ReadArtifacts(location, generationRoot, manifest.Artifacts);
        var policy = ResearchWorkspaceAuthorityArtifacts.VerifyPolicyCanonicalRecord(artifactBytes["authority-policy"]);
        if (policy.PolicyDigest != predecessor.Policy.PolicyDigest ||
            !string.Equals(manifest.AuthorityPolicyId, policy.PolicyId, StringComparison.Ordinal) ||
            !string.Equals(manifest.AuthorityPolicyDigest, policy.PolicyDigest.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException("FE-02 policy rotation or manifest policy mismatch is not admitted.");
        var target = ResolveTarget(sourceResult, artifactBytes["review-command"]);
        var command = ResearchWorkspaceAuthorityArtifacts.VerifyReviewCommandCanonicalRecord(
            artifactBytes["review-command"], policy, sourceResult, target, predecessor.CurrentSnapshot.DecisionSetDigest,
            predecessor.GenerationId, ContentDigest.Parse(manifest.PredecessorAuthorityGenerationManifestSha256),
            predecessor.CurrentSnapshot.SnapshotId, predecessor.CurrentSnapshot.RecordDigest);
        var decision = ResearchWorkspaceAuthorityArtifacts.VerifyDecisionCanonicalRecord(artifactBytes["decision"], policy, sourceResult, target);
        if (!string.Equals(decision.DecisionId, command.DecisionId, StringComparison.Ordinal)) throw new InvalidOperationException("Request and decision identities disagree.");
        var active = predecessor.ActiveDecisions
            .Where(item => command.Material.SupersedesDecisionId is null || !string.Equals(item.DecisionId, command.Material.SupersedesDecisionId, StringComparison.Ordinal))
            .Append(decision).ToArray();
        var knownDecisions = predecessor.KnownDecisions.Append(decision).ToArray();
        var successor = ResearchWorkspaceAuthorityArtifacts.VerifySuccessorSnapshotCanonicalRecord(
            artifactBytes["successor-snapshot"], sourceResult, policy, predecessor.CurrentSnapshot,
            active, knownDecisions, predecessor.KnownSnapshots, decision);
        var knownSnapshots = predecessor.KnownSnapshots.Append(successor).ToArray();
        var invalidation = ResearchWorkspaceAuthorityArtifacts.VerifyInvalidationCanonicalRecord(
            artifactBytes["invalidation"], policy, decision, successor, knownDecisions, knownSnapshots);
        var decisionEvent = ResearchWorkspaceAuthorityArtifacts.VerifyResearchEventCanonicalRecord(artifactBytes["decision-recorded-event"]);
        var invalidatedEvent = ResearchWorkspaceAuthorityArtifacts.VerifyResearchEventCanonicalRecord(artifactBytes["snapshot-invalidated-event"]);
        var publicationEvent = ResearchWorkspaceAuthorityArtifacts.VerifyResearchEventCanonicalRecord(artifactBytes["snapshot-publication-event"]);

        VerifyManifestRecords(manifest, command, target, sourceResult, policy, decision, predecessor.CurrentSnapshot, successor, invalidation,
            decisionEvent, invalidatedEvent, publicationEvent);
        var transition = new ResearchWorkspaceVerifiedAuthorityTransition(manifest, command, decision, successor, invalidation,
            decisionEvent, invalidatedEvent, publicationEvent);
        return new ResearchWorkspaceVerifiedAuthorityChain(manifest.AuthorityGenerationId, manifest.ProjectRevision, policy, successor,
            publicationEvent, active, knownDecisions, knownSnapshots, predecessor.Transitions.Append(transition).ToArray());
    }

    private static VerifiedDeduplicationAuthorityReviewTargetDigest ResolveTarget(
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        byte[] requestBytes)
    {
        using var document = JsonDocument.Parse(requestBytes);
        var targetId = document.RootElement.GetProperty("target_id").GetString();
        var targetDigest = ContentDigest.Parse(document.RootElement.GetProperty("target_digest").GetString()!);
        foreach (var pair in sourceResult.Result.ReviewRequiredCandidates)
        {
            var ids = new[] { pair.CandidateAId, pair.CandidateBId }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var evidence = sourceResult.Result.Evidence.Where(item => item.ObjectCandidateId is not null &&
                ids.Contains(item.SubjectCandidateId, StringComparer.Ordinal) && ids.Contains(item.ObjectCandidateId, StringComparer.Ordinal) &&
                !string.Equals(item.SubjectCandidateId, item.ObjectCandidateId, StringComparison.Ordinal)).ToArray();
            var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(sourceResult, pair, ids, evidence);
            if (string.Equals(target.TargetId, targetId, StringComparison.Ordinal) && target.TargetDigest == targetDigest) return target;
        }
        throw new InvalidOperationException("Persisted review command target cannot be resolved.");
    }

    private static Dictionary<string, byte[]> ReadArtifacts(ResearchWorkspaceLocation location, JsonElement entries, string[] expected)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var array = entries.EnumerateArray().ToArray();
        if (!array.Select(item => item.GetProperty("name").GetString()).SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException("Baseline artifact set is invalid.");
        foreach (var item in array)
        {
            var path = Resolve(location, item.GetProperty("relative_path").GetString()!);
            var bytes = File.ReadAllBytes(path);
            if (ContentDigest.Sha256(bytes).ToString() != item.GetProperty("sha256").GetString()) throw new InvalidOperationException("Authority artifact digest mismatch.");
            result.Add(item.GetProperty("name").GetString()!, bytes);
        }
        return result;
    }

    private static Dictionary<string, byte[]> ReadArtifacts(
        ResearchWorkspaceLocation location,
        string generationRoot,
        IReadOnlyList<ResearchWorkspaceGenerationArtifact> entries)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var generationAbsolute = Path.GetFullPath(generationRoot);
        var generationWithSeparator = generationAbsolute.EndsWith(Path.DirectorySeparatorChar)
            ? generationAbsolute
            : generationAbsolute + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var expectedPrefix =
            ResearchWorkspacePaths.AuthorityGenerationRoot(Path.GetFileName(generationRoot)) + "/";
        foreach (var item in entries)
        {
            if (!item.RelativePath.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Authority artifact path is not bound to its generation.");
            }

            var localRelativePath = item.RelativePath[expectedPrefix.Length..];
            if (localRelativePath.Length == 0 ||
                localRelativePath.Contains('/') ||
                localRelativePath.Contains('\\'))
            {
                throw new InvalidOperationException("Authority artifact path is not bound to its generation.");
            }

            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, item.RelativePath, out var fullPath))
            {
                throw new InvalidOperationException("Authority artifact path is not bound to its generation.");
            }

            if (!fullPath.StartsWith(generationWithSeparator, comparison))
            {
                throw new InvalidOperationException("Authority artifact path is not bound to its generation.");
            }

            var relativeFromGeneration = Path.GetRelativePath(generationAbsolute, fullPath).Replace('\\', '/');
            if (!string.Equals(relativeFromGeneration, localRelativePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Authority artifact path is not bound to its generation.");
            }

            var bytes = File.ReadAllBytes(fullPath);
            if (ContentDigest.Sha256(bytes).ToString() != item.Sha256) throw new InvalidOperationException("Authority artifact digest mismatch.");
            result.Add(item.Name, bytes);
        }
        return result;
    }

    private static void VerifyManifestRecords(
        ResearchWorkspaceSuccessorAuthorityGenerationManifest manifest,
        VerifiedDeduplicationReviewCommand command,
        VerifiedDeduplicationAuthorityReviewTargetDigest target,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityDecision decision,
        VerifiedCorpusSnapshot predecessor,
        VerifiedCorpusSnapshot successor,
        VerifiedCorpusSnapshotInvalidation invalidation,
        ResearchEvent decisionEvent,
        ResearchEvent invalidatedEvent,
        ResearchEvent publicationEvent)
    {
        var checks = new[]
        {
            (manifest.RequestId, command.RequestId), (manifest.RequestDigest, command.RequestDigest.ToString()),
            (manifest.DecisionId, decision.DecisionId), (manifest.DecisionDigest, decision.DecisionDigest.ToString()),
            (manifest.PredecessorSnapshotId, predecessor.SnapshotId), (manifest.PredecessorSnapshotRecordDigest, predecessor.RecordDigest.ToString()),
            (manifest.SuccessorSnapshotId, successor.SnapshotId), (manifest.SuccessorSnapshotContentDigest, successor.ContentDigest.ToString()),
            (manifest.SuccessorSnapshotRecordDigest, successor.RecordDigest.ToString()), (manifest.InvalidationId, invalidation.InvalidationId),
            (manifest.InvalidationRecordDigest, invalidation.RecordDigest.ToString()), (manifest.DecisionSetDigest, successor.DecisionSetDigest.ToString()),
            (manifest.DecisionRecordedEventDigest, decisionEvent.EventDigest.ToString()),
            (manifest.SnapshotInvalidatedEventDigest, invalidatedEvent.EventDigest.ToString()),
            (manifest.SnapshotPublicationEventDigest, publicationEvent.EventDigest.ToString())
        };
        if (checks.Any(item => !string.Equals(item.Item1, item.Item2, StringComparison.Ordinal))) throw new InvalidOperationException("Successor manifest and authority records disagree.");
        var agent = new ProvenanceAgent(command.Material.ActorId, ProvenanceAgent.HumanKind);
        var commandRef = new ProvenanceEntityRef(DeduplicationReviewCommandConstants.SchemaId, command.RequestId, command.RequestDigest);
        var targetRef = new ProvenanceEntityRef(target.TargetKind, target.TargetId, target.TargetDigest);
        var resultRef = new ProvenanceEntityRef("nexus.deduplication.result", sourceResult.Result.ResultId, sourceResult.ResultDigest);
        var policyRef = new ProvenanceEntityRef(DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, policy.PolicyId, policy.PolicyDigest);
        var predecessorRef = new ProvenanceEntityRef("nexus.corpus.snapshot", predecessor.SnapshotId, predecessor.RecordDigest);
        var decisionRef = new ProvenanceEntityRef(DeduplicationDecisionConstants.SchemaId, decision.DecisionId, decision.DecisionDigest);
        var successorRef = new ProvenanceEntityRef("nexus.corpus.snapshot", successor.SnapshotId, successor.RecordDigest);
        var invalidationRef = new ProvenanceEntityRef(CorpusSnapshotInvalidationConstants.SchemaId, invalidation.InvalidationId, invalidation.RecordDigest);
        var decisionSetRef = new ProvenanceEntityRef("deduplication-decision-set", $"decision-set-{successor.SnapshotId}", successor.DecisionSetDigest);
        if (decisionEvent.Activity.ActivityId != "deduplication-decision-recorded" ||
            decisionEvent.Subject != decisionRef || decisionEvent.Agent != agent ||
            !decisionEvent.Inputs.SequenceEqual(new[] { commandRef, targetRef, resultRef, policyRef, predecessorRef }) ||
            !decisionEvent.Outputs.SequenceEqual(new[] { decisionRef }) ||
            invalidatedEvent.Activity.ActivityId != "corpus-snapshot-invalidated" ||
            invalidatedEvent.Subject != predecessorRef || invalidatedEvent.Agent != agent ||
            !invalidatedEvent.Inputs.SequenceEqual(new[] { decisionRef, predecessorRef, successorRef }) ||
            !invalidatedEvent.Outputs.SequenceEqual(new[] { invalidationRef }) ||
            publicationEvent.Activity.ActivityId != "corpus-snapshot-published" ||
            publicationEvent.Subject != successorRef || publicationEvent.Agent != agent ||
            !publicationEvent.Inputs.SequenceEqual(new[] { decisionRef, predecessorRef, invalidationRef, policyRef, decisionSetRef }) ||
            !publicationEvent.Outputs.SequenceEqual(new[] { successorRef }) ||
            new[] { decisionEvent.EventId, invalidatedEvent.EventId, publicationEvent.EventId }.Distinct().Count() != 3 ||
            decisionEvent.ProtocolBinding is not null || invalidatedEvent.ProtocolBinding is not null || publicationEvent.ProtocolBinding is not null ||
            decisionEvent.WorkflowBinding is not null || invalidatedEvent.WorkflowBinding is not null || publicationEvent.WorkflowBinding is not null)
            throw new InvalidOperationException("Successor provenance activities are invalid.");
    }

    private static bool HasBaselinePublicationBinding(
        ResearchEvent publicationEvent,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedCorpusSnapshot snapshot,
        string analysisGenerationId,
        ContentDigest analysisManifestDigest)
    {
        var snapshotRef = new ProvenanceEntityRef("nexus.corpus.snapshot", snapshot.SnapshotId, snapshot.RecordDigest);
        var expectedInputs = new[]
        {
            new ProvenanceEntityRef("nexus.deduplication.result", sourceResult.Result.ResultId, sourceResult.ResultDigest),
            new ProvenanceEntityRef(DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, policy.PolicyId, policy.PolicyDigest),
            new ProvenanceEntityRef("source-analysis-manifest", analysisGenerationId, analysisManifestDigest),
            new ProvenanceEntityRef("deduplication-decision-set", "decision-set-empty", snapshot.DecisionSetDigest)
        };
        return string.Equals(publicationEvent.Activity.ActivityId, "corpus-snapshot-published", StringComparison.Ordinal) &&
            publicationEvent.Agent.AgentKind == ProvenanceAgent.HumanKind &&
            string.Equals(publicationEvent.Agent.AgentId, snapshot.CreatedByActorId, StringComparison.Ordinal) &&
            publicationEvent.Subject == snapshotRef &&
            publicationEvent.Outputs.SequenceEqual(new[] { snapshotRef }) &&
            publicationEvent.Inputs.SequenceEqual(expectedInputs) &&
            publicationEvent.ProtocolBinding is null && publicationEvent.WorkflowBinding is null;
    }

    private static string Resolve(ResearchWorkspaceLocation location, string relativePath)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var path) || !File.Exists(path))
            throw new InvalidOperationException("Authority generation file is missing or outside workspace.");
        return path;
    }

    private static void Require(JsonElement root, string name, string expected)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || value.GetString() != expected)
            throw new InvalidOperationException($"Authority manifest field '{name}' is invalid.");
    }
}

public sealed record ResearchWorkspaceVerifiedAuthorityTransition(
    ResearchWorkspaceSuccessorAuthorityGenerationManifest Manifest,
    VerifiedDeduplicationReviewCommand Command,
    VerifiedDeduplicationAuthorityDecision Decision,
    VerifiedCorpusSnapshot Snapshot,
    VerifiedCorpusSnapshotInvalidation Invalidation,
    ResearchEvent DecisionRecordedEvent,
    ResearchEvent SnapshotInvalidatedEvent,
    ResearchEvent SnapshotPublicationEvent);

public sealed record ResearchWorkspaceVerifiedAuthorityChain(
    string GenerationId,
    long ProjectRevision,
    VerifiedDeduplicationAuthorityPolicy Policy,
    VerifiedCorpusSnapshot CurrentSnapshot,
    ResearchEvent CurrentPublicationEvent,
    IReadOnlyList<VerifiedDeduplicationAuthorityDecision> ActiveDecisions,
    IReadOnlyList<VerifiedDeduplicationAuthorityDecision> KnownDecisions,
    IReadOnlyList<VerifiedCorpusSnapshot> KnownSnapshots,
    IReadOnlyList<ResearchWorkspaceVerifiedAuthorityTransition> Transitions);
