using System.Globalization;
using System.Text.Json;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;
using NexusScholar.Screening.WorkflowExecution;
using NexusScholar.WorkflowExecution;

namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceScreeningConductManifest(
    string Schema,
    string GenerationId,
    string WorkspaceId,
    long ProjectRevision,
    string ConductId,
    string PolicyDigest,
    string HeaderDigest,
    string PriorHeadDigest,
    string ResultingHeadDigest,
    int EntryCount,
    int DecisionCount,
    int InvalidationCount,
    string? HandoffId,
    string? HandoffDigest,
    string? MatchingWorkflowEventDigest,
    string? WorkflowGenerationId,
    string? WorkflowManifestSha256,
    string? PredecessorGenerationId,
    string? PredecessorManifestSha256,
    IReadOnlyList<ResearchWorkspaceGenerationArtifact> Artifacts)
{
    public const string CurrentSchema = "nexus.workspace-screening-conduct-generation.v1";
}

public static class ResearchWorkspaceScreeningConductManifestCodec
{
    public static byte[] Serialize(ResearchWorkspaceScreeningConductManifest manifest)
    {
        Validate(manifest);
        var value = new CanonicalJsonObject()
            .Add("schema", manifest.Schema).Add("generation_id", manifest.GenerationId).Add("workspace_id", manifest.WorkspaceId)
            .Add("project_revision", manifest.ProjectRevision).Add("conduct_id", manifest.ConductId)
            .Add("policy_digest", manifest.PolicyDigest).Add("header_digest", manifest.HeaderDigest)
            .Add("prior_head_digest", manifest.PriorHeadDigest).Add("resulting_head_digest", manifest.ResultingHeadDigest)
            .Add("entry_count", manifest.EntryCount).Add("decision_count", manifest.DecisionCount)
            .Add("invalidation_count", manifest.InvalidationCount)
            .Add("artifacts", CanonicalJsonValue.Array(manifest.Artifacts.OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => new CanonicalJsonObject().Add("name", item.Name).Add("relative_path", item.RelativePath).Add("sha256", item.Sha256)).ToArray()));
        AddPair(value, "handoff_id", manifest.HandoffId, "handoff_digest", manifest.HandoffDigest);
        if (manifest.MatchingWorkflowEventDigest is not null) value.Add("matching_workflow_event_digest", manifest.MatchingWorkflowEventDigest);
        AddPair(value, "workflow_generation_id", manifest.WorkflowGenerationId, "workflow_manifest_sha256", manifest.WorkflowManifestSha256);
        AddPair(value, "predecessor_generation_id", manifest.PredecessorGenerationId,
            "predecessor_manifest_sha256", manifest.PredecessorManifestSha256);
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(value);
    }

    public static ResearchWorkspaceScreeningConductManifest Rehydrate(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        if (CanonicalJsonValue.FromJsonElement(document.RootElement) is not CanonicalJsonObject root ||
            !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
            throw new InvalidOperationException("Screening conduct manifest must use canonical JSON bytes.");
        var required = new[] { "artifacts", "conduct_id", "decision_count", "entry_count", "generation_id", "header_digest",
            "invalidation_count", "policy_digest", "prior_head_digest", "project_revision", "resulting_head_digest", "schema", "workspace_id" };
        var allowed = required.Concat(new[] { "handoff_id", "handoff_digest", "matching_workflow_event_digest",
            "workflow_generation_id", "workflow_manifest_sha256", "predecessor_generation_id", "predecessor_manifest_sha256" }).ToHashSet(StringComparer.Ordinal);
        if (!required.All(root.Properties.ContainsKey) || root.Properties.Keys.Any(key => !allowed.Contains(key)))
            throw new InvalidOperationException("Screening conduct manifest has missing or unknown fields.");
        var manifest = new ResearchWorkspaceScreeningConductManifest(
            Text(root, "schema"), Text(root, "generation_id"), Text(root, "workspace_id"), Number(root, "project_revision"),
            Text(root, "conduct_id"), Text(root, "policy_digest"), Text(root, "header_digest"), Text(root, "prior_head_digest"),
            Text(root, "resulting_head_digest"), checked((int)Number(root, "entry_count")), checked((int)Number(root, "decision_count")),
            checked((int)Number(root, "invalidation_count")), OptionalText(root, "handoff_id"), OptionalText(root, "handoff_digest"),
            OptionalText(root, "matching_workflow_event_digest"), OptionalText(root, "workflow_generation_id"),
            OptionalText(root, "workflow_manifest_sha256"), OptionalText(root, "predecessor_generation_id"),
            OptionalText(root, "predecessor_manifest_sha256"), Array(root, "artifacts").Select(ParseArtifact).ToArray());
        Validate(manifest);
        return manifest;
    }

    private static void Validate(ResearchWorkspaceScreeningConductManifest value)
    {
        if (value.Schema != ResearchWorkspaceScreeningConductManifest.CurrentSchema || value.ProjectRevision <= 0 ||
            value.EntryCount < 0 || value.DecisionCount < 0 || value.InvalidationCount < 0 ||
            value.DecisionCount + value.InvalidationCount != value.EntryCount || value.Artifacts.Count != value.EntryCount + 2 + (value.HandoffId is null ? 0 : 1) ||
            value.Artifacts.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != value.Artifacts.Count ||
            (value.HandoffId is null) != (value.HandoffDigest is null) ||
            (value.WorkflowGenerationId is null) != (value.WorkflowManifestSha256 is null) ||
            (value.MatchingWorkflowEventDigest is null) != (value.WorkflowGenerationId is null) ||
            (value.PredecessorGenerationId is null) != (value.PredecessorManifestSha256 is null))
            throw new InvalidOperationException("Screening conduct manifest shape is invalid.");
        foreach (var digest in new[] { value.PolicyDigest, value.HeaderDigest, value.PriorHeadDigest, value.ResultingHeadDigest }
            .Concat(Optional(value.HandoffDigest)).Concat(Optional(value.MatchingWorkflowEventDigest)).Concat(Optional(value.WorkflowManifestSha256)).Concat(Optional(value.PredecessorManifestSha256))
            .Concat(value.Artifacts.Select(item => item.Sha256))) _ = ContentDigest.Parse(digest);
    }

    private static IEnumerable<string> Optional(string? value) => value is null ? System.Array.Empty<string>() : [value];
    private static void AddPair(CanonicalJsonObject value, string firstName, string? first, string secondName, string? second)
    { if (first is not null) value.Add(firstName, first).Add(secondName, second!); }
    private static ResearchWorkspaceGenerationArtifact ParseArtifact(CanonicalJsonValue value)
    {
        if (value is not CanonicalJsonObject item || item.Properties.Count != 3) throw new InvalidOperationException("Screening artifact entry is invalid.");
        return new ResearchWorkspaceGenerationArtifact(Text(item, "name"), Text(item, "relative_path"), Text(item, "sha256"));
    }
    private static string Text(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonString text
        ? text.Value : throw new InvalidOperationException($"Screening manifest field '{name}' must be a string.");
    private static string? OptionalText(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Text(root, name) : null;
    private static long Number(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonNumber number &&
        long.TryParse(number.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : throw new InvalidOperationException($"Screening manifest field '{name}' must be an integer.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonArray array
        ? array.Items : throw new InvalidOperationException($"Screening manifest field '{name}' must be an array.");
}

public sealed record VerifiedResearchWorkspaceScreeningConduct(
    ResearchWorkspaceScreeningConductManifest Manifest,
    ScreeningConductPolicy Policy,
    ScreeningConductHeader Header,
    IReadOnlyList<IScreeningConductEntry> Entries,
    ScreeningConductJournal Journal,
    ScreeningConductHandoff? Handoff);

public static class ResearchWorkspaceScreeningConductVerifier
{
    public static VerifiedResearchWorkspaceScreeningConduct VerifyCurrent(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        VerifiedDeduplicationResult deduplication,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria)
    {
        if (project.CurrentScreeningConductGenerationId is null || project.ScreeningConductManifestPath is null || project.ScreeningConductManifestSha256 is null)
            throw new InvalidOperationException("The workspace has no current Screening conduct generation.");
        var manifestPath = Resolve(location, project.ScreeningConductManifestPath);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        if (ContentDigest.Sha256(manifestBytes).ToString() != project.ScreeningConductManifestSha256)
            throw new InvalidOperationException("Screening conduct manifest failed project-pointer digest verification.");
        var manifest = ResearchWorkspaceScreeningConductManifestCodec.Rehydrate(manifestBytes);
        if (manifest.GenerationId != project.CurrentScreeningConductGenerationId || manifest.WorkspaceId != project.WorkspaceId || manifest.ProjectRevision != project.Revision)
            throw new InvalidOperationException("Screening conduct manifest is stale or bound to another workspace.");
        if (manifest.WorkflowGenerationId is not null &&
            (manifest.WorkflowGenerationId != project.CurrentWorkflowExecutionJournalGenerationId ||
            manifest.WorkflowManifestSha256 != project.WorkflowExecutionJournalManifestSha256))
            throw new InvalidOperationException("Screening conduct generation is not paired with the current Workflow execution generation.");
        var bytesByName = manifest.Artifacts.ToDictionary(item => item.Name, item =>
        {
            var bytes = File.ReadAllBytes(Resolve(location, item.RelativePath));
            if (ContentDigest.Sha256(bytes).ToString() != item.Sha256) throw new InvalidOperationException($"Screening artifact '{item.Name}' failed digest verification.");
            return bytes;
        }, StringComparer.Ordinal);
        var policy = ScreeningConductCanonicalCodec.RehydratePolicy(bytesByName["conduct-policy"], ContentDigest.Parse(manifest.PolicyDigest), deduplication, protocol, criteria);
        var header = ScreeningConductCanonicalCodec.RehydrateHeader(bytesByName["header"], ContentDigest.Parse(manifest.HeaderDigest), policy);
        var entries = new List<IScreeningConductEntry>();
        for (var index = 1; index <= manifest.EntryCount; index++)
        {
            var bytes = bytesByName[$"entry-{index:D6}"];
            using var document = JsonDocument.Parse(bytes);
            var schema = document.RootElement.GetProperty("schema").GetString();
            var digest = ContentDigest.Sha256(bytes);
            entries.Add(schema switch
            {
                ScreeningConductDecision.SchemaId => ScreeningConductCanonicalCodec.RehydrateDecision(bytes, digest, header),
                ScreeningConductInvalidation.SchemaId => ScreeningConductCanonicalCodec.RehydrateInvalidation(bytes, digest, header),
                _ => throw new InvalidOperationException("Unknown Screening conduct entry schema.")
            });
        }
        var journal = ScreeningConductJournal.RehydrateEntries(header, policy, entries);
        if (journal.Projection.HeadDigest.ToString() != manifest.ResultingHeadDigest || journal.Decisions.Count != manifest.DecisionCount ||
            journal.Invalidations.Count != manifest.InvalidationCount)
            throw new InvalidOperationException("Screening conduct replay does not match its manifest.");
        ScreeningConductHandoff? handoff = null;
        if (manifest.HandoffDigest is not null)
            handoff = ScreeningConductCanonicalCodec.RehydrateHandoff(bytesByName["handoff"], ContentDigest.Parse(manifest.HandoffDigest), journal);
        return new VerifiedResearchWorkspaceScreeningConduct(manifest, policy, header, entries.AsReadOnly(), journal, handoff);
    }

    private static string Resolve(ResearchWorkspaceLocation location, string relative)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relative, out var path) || !File.Exists(path))
            throw new InvalidOperationException("Screening conduct file is missing or outside the workspace.");
        return path;
    }
}

public static class ResearchWorkspaceScreeningConductTransaction
{
    public static ResearchWorkspaceScreeningConductCommit Commit(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        VerifiedDeduplicationResult deduplication,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria,
        ScreeningConductPolicy policy,
        ScreeningConductHeader header,
        IReadOnlyList<IScreeningConductEntry> entries,
        ScreeningConductHandoff? handoff = null,
        ResearchWorkspacePreparedWorkflowExecutionGeneration? preparedWorkflow = null,
        ScreeningConductDecision? matchingDecision = null,
        WorkflowExecutionEvent? matchingWorkflowEvent = null,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        var journal = ScreeningConductJournal.RehydrateEntries(header, policy, entries);
        if (handoff is not null) _ = ScreeningConductCanonicalCodec.RehydrateHandoff(ScreeningConductCanonicalCodec.Serialize(handoff), handoff.Digest, journal);
        if (preparedWorkflow is not null)
        {
            if (matchingDecision is null || matchingWorkflowEvent is null ||
                !preparedWorkflow.Journal.Events.Any(item => item.Digest == matchingWorkflowEvent.Digest))
                throw new InvalidOperationException("Combined Screening commit requires the exact prepared Workflow completion event.");
            var reference = ScreeningWorkflowExecutionBridge.CreateHumanTaskDecisionReference(journal, matchingDecision, matchingWorkflowEvent.Actor);
            if (matchingWorkflowEvent.Decision != reference || matchingWorkflowEvent.Kind != WorkflowExecutionEventKind.WorkCompleted)
                throw new InvalidOperationException("Prepared Workflow event does not complete work with the matching Screening decision.");
        }
        VerifiedResearchWorkspaceScreeningConduct? predecessor = null;
        if (expectedProject.CurrentScreeningConductGenerationId is not null)
        {
            predecessor = ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(location, expectedProject, deduplication, protocol, criteria);
            if (predecessor.Journal.Projection.HeadDigest == journal.Projection.HeadDigest && predecessor.Handoff?.Digest == handoff?.Digest &&
                (preparedWorkflow is null ||
                expectedProject.CurrentWorkflowExecutionJournalGenerationId == preparedWorkflow.Manifest.GenerationId &&
                expectedProject.WorkflowExecutionJournalManifestSha256 == preparedWorkflow.ManifestDigest.ToString()))
                return new ResearchWorkspaceScreeningConductCommit(expectedProject, predecessor.Manifest, predecessor.Journal, predecessor.Handoff, true);
            if (predecessor.Entries.Count > entries.Count || !predecessor.Entries.Select(item => item.Digest).SequenceEqual(entries.Take(predecessor.Entries.Count).Select(item => item.Digest)))
                throw new ResearchWorkspaceConcurrencyException("Screening conduct does not extend current persisted history.", new InvalidOperationException());
        }
        var stateDigest = ContentDigest.Sha256Utf8($"{journal.Projection.HeadDigest}|{handoff?.Digest}");
        var generationId = $"screening-{stateDigest.Value[7..23]}-{entries.Count:D6}";
        var relativeRoot = ResearchWorkspacePaths.ScreeningConductRoot(header.ConductId, generationId);
        var finalRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, relativeRoot);
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, $"{ResearchWorkspacePaths.GenerationStaging}/{generationId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        try
        {
            var records = new List<(string Name, string FileName, byte[] Bytes)>
            {
                ("conduct-policy", "conduct-policy.json", ScreeningConductCanonicalCodec.Serialize(policy)),
                ("header", "header.json", ScreeningConductCanonicalCodec.Serialize(header))
            };
            records.AddRange(entries.Select((entry, index) => ($"entry-{index + 1:D6}", $"entry-{index + 1:D6}.json", Serialize(entry))));
            if (handoff is not null) records.Add(("handoff", "handoff.json", ScreeningConductCanonicalCodec.Serialize(handoff)));
            var artifacts = records.Select(record =>
            {
                File.WriteAllBytes(Path.Combine(stagingRoot, record.FileName), record.Bytes);
                return new ResearchWorkspaceGenerationArtifact(record.Name, $"{relativeRoot}/{record.FileName}", ContentDigest.Sha256(record.Bytes).ToString());
            }).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            var manifestPath = $"{relativeRoot}/screening-conduct.manifest.json";
            var placeholder = "sha256:" + new string('0', 64);
            var committed = preparedWorkflow is null
                ? expectedProject.CommitScreeningConductGeneration(generationId, manifestPath, placeholder)
                : expectedProject.CommitScreeningAndWorkflowExecutionGenerations(
                    generationId, manifestPath, placeholder, preparedWorkflow.Manifest.GenerationId,
                    preparedWorkflow.ManifestPath, preparedWorkflow.ManifestDigest.ToString());
            var manifest = new ResearchWorkspaceScreeningConductManifest(
                ResearchWorkspaceScreeningConductManifest.CurrentSchema, generationId, expectedProject.WorkspaceId, committed.Revision,
                header.ConductId, policy.Digest.ToString(), header.Digest.ToString(), predecessor?.Journal.Projection.HeadDigest.ToString() ?? header.Digest.ToString(),
                journal.Projection.HeadDigest.ToString(), entries.Count, journal.Decisions.Count, journal.Invalidations.Count,
                handoff?.HandoffId, handoff?.Digest.ToString(), matchingWorkflowEvent?.Digest.ToString(),
                preparedWorkflow?.Manifest.GenerationId, preparedWorkflow?.ManifestDigest.ToString(),
                predecessor?.Manifest.GenerationId, expectedProject.ScreeningConductManifestSha256, artifacts);
            var manifestBytes = ResearchWorkspaceScreeningConductManifestCodec.Serialize(manifest);
            committed = committed with { ScreeningConductManifestSha256 = ContentDigest.Sha256(manifestBytes).ToString() };
            File.WriteAllBytes(Path.Combine(stagingRoot, "screening-conduct.manifest.json"), manifestBytes);
            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterStaging);
            Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
            using var workspaceLock = AcquireLock(location);
            var current = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (current.Revision != expectedProject.Revision || current.CurrentScreeningConductGenerationId != expectedProject.CurrentScreeningConductGenerationId ||
                current.ScreeningConductManifestSha256 != expectedProject.ScreeningConductManifestSha256 ||
                current.CurrentWorkflowExecutionJournalGenerationId != expectedProject.CurrentWorkflowExecutionJournalGenerationId ||
                current.WorkflowExecutionJournalManifestSha256 != expectedProject.WorkflowExecutionJournalManifestSha256)
                throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, current.Revision);
            var workflowPromoted = false;
            try
            {
                if (preparedWorkflow is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(preparedWorkflow.FinalRoot)!);
                    if (Directory.Exists(preparedWorkflow.FinalRoot))
                    {
                        if (!Matches(preparedWorkflow.StagingRoot, preparedWorkflow.FinalRoot))
                            throw new ResearchWorkspaceConcurrencyException(
                                "Prepared Workflow generation identity collides with different bytes.", new InvalidOperationException());
                        Directory.Delete(preparedWorkflow.StagingRoot, true);
                    }
                    else
                    {
                        Directory.Move(preparedWorkflow.StagingRoot, preparedWorkflow.FinalRoot);
                        workflowPromoted = true;
                    }
                }
                if (Directory.Exists(finalRoot))
                {
                    if (!Matches(stagingRoot, finalRoot)) { Quarantine(location, finalRoot, generationId); Directory.Move(stagingRoot, finalRoot); }
                }
                else Directory.Move(stagingRoot, finalRoot);
                faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterPromotion);
                ResearchWorkspaceStore.WriteProject(location, committed);
            }
            catch
            {
                Quarantine(location, finalRoot, generationId);
                if (workflowPromoted)
                    Quarantine(location, preparedWorkflow!.FinalRoot, preparedWorkflow.Manifest.GenerationId);
                throw;
            }
            var verified = ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(location, committed, deduplication, protocol, criteria);
            if (preparedWorkflow is not null)
                _ = ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(
                    location, committed, preparedWorkflow.Workflow, preparedWorkflow.RecordResolver);
            return new ResearchWorkspaceScreeningConductCommit(committed, verified.Manifest, verified.Journal, verified.Handoff, false);
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            if (preparedWorkflow is not null && Directory.Exists(preparedWorkflow.StagingRoot))
                Directory.Delete(preparedWorkflow.StagingRoot, true);
        }
    }

    private static byte[] Serialize(IScreeningConductEntry entry) => entry switch
    {
        ScreeningConductDecision decision => ScreeningConductCanonicalCodec.Serialize(decision),
        ScreeningConductInvalidation invalidation => ScreeningConductCanonicalCodec.Serialize(invalidation),
        _ => throw new InvalidOperationException("Unknown Screening conduct entry type.")
    };
    private static FileStream AcquireLock(ResearchWorkspaceLocation location) => new(
        Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private static bool Matches(string left, string right)
    {
        var files = Directory.GetFiles(left, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(left, path), File.ReadAllBytes, StringComparer.Ordinal);
        var other = Directory.GetFiles(right, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(right, path), File.ReadAllBytes, StringComparer.Ordinal);
        return files.Count == other.Count && files.All(pair => other.TryGetValue(pair.Key, out var bytes) && pair.Value.SequenceEqual(bytes));
    }
    private static void Quarantine(ResearchWorkspaceLocation location, string root, string generationId)
    {
        if (!Directory.Exists(root)) return;
        var target = ResearchWorkspacePaths.InProject(location.RootDirectory, $"{ResearchWorkspacePaths.GenerationQuarantine}/{generationId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!); Directory.Move(root, target);
    }
}

public sealed record ResearchWorkspaceScreeningConductCommit(
    ResearchWorkspaceProject Project,
    ResearchWorkspaceScreeningConductManifest Manifest,
    ScreeningConductJournal Journal,
    ScreeningConductHandoff? Handoff,
    bool AlreadyApplied);

public sealed class ResearchWorkspaceScreeningConductCommitPort(
    ResearchWorkspaceLocation location,
    ResearchWorkspaceProject expectedProject,
    VerifiedDeduplicationResult deduplication,
    VerifiedProtocolVersion protocol,
    ScreeningCriteria criteria) : IScreeningConductCommitPort
{
    public ScreeningConductCommitResult Commit(ScreeningConductPolicy policy, ScreeningConductHeader header, IReadOnlyList<IScreeningConductEntry> entries)
    {
        var result = ResearchWorkspaceScreeningConductTransaction.Commit(location, expectedProject, deduplication, protocol, criteria, policy, header, entries);
        return new ScreeningConductCommitResult(header.ConductId, result.Journal.Projection.HeadDigest, entries.Count, result.AlreadyApplied);
    }
}
