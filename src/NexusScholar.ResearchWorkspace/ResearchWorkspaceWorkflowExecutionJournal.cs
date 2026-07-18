using NexusScholar.AppServices;
using NexusScholar.Kernel;
using NexusScholar.Workflow;
using NexusScholar.WorkflowExecution;

namespace NexusScholar.ResearchWorkspace;

public sealed record VerifiedResearchWorkspaceWorkflowExecutionJournal(
    ResearchWorkspaceWorkflowExecutionJournalManifest Manifest,
    WorkflowExecutionAuthorityPolicy Policy,
    WorkflowExecutionHeader Header,
    IReadOnlyList<WorkflowExecutionEvent> Events,
    WorkflowExecutionJournal Journal);

public static class ResearchWorkspaceWorkflowExecutionJournalVerifier
{
    public static VerifiedResearchWorkspaceWorkflowExecutionJournal VerifyCurrent(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        VerifiedWorkflowDefinition workflow,
        IWorkflowExecutionRecordResolver recordResolver)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(workflow);
        if (project.CurrentWorkflowExecutionJournalGenerationId is null ||
            project.WorkflowExecutionJournalManifestPath is null ||
            project.WorkflowExecutionJournalManifestSha256 is null)
            throw new InvalidOperationException("Workspace has no current Workflow execution journal generation.");

        var manifestPath = Resolve(location, project.WorkflowExecutionJournalManifestPath);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        if (ContentDigest.Sha256(manifestBytes) != ContentDigest.Parse(project.WorkflowExecutionJournalManifestSha256))
            throw new InvalidOperationException("Workflow execution journal manifest digest does not match the project pointer.");
        var manifest = ResearchWorkspaceWorkflowExecutionJournalManifestCodec.Rehydrate(manifestBytes);
        if (manifest.GenerationId != project.CurrentWorkflowExecutionJournalGenerationId ||
            manifest.WorkspaceId != project.WorkspaceId || manifest.ProjectRevision != project.Revision ||
            manifest.WorkflowId != workflow.Definition.WorkflowId ||
            ContentDigest.Parse(manifest.WorkflowDigest) != workflow.Definition.WorkflowDigest)
            throw new InvalidOperationException("Workflow execution journal manifest does not bind the current workspace and verified Workflow.");

        VerifyPredecessor(location, manifest);
        var bytes = manifest.Artifacts.ToDictionary(
            artifact => artifact.Name,
            artifact => ReadArtifact(location, artifact),
            StringComparer.Ordinal);
        var policy = WorkflowExecutionCanonicalCodec.RehydratePolicy(
            Require(bytes, "authority-policy"), ContentDigest.Parse(manifest.AuthorityPolicyDigest), workflow);
        var header = WorkflowExecutionCanonicalCodec.RehydrateHeader(
            Require(bytes, "header"), ContentDigest.Parse(manifest.HeaderDigest), workflow, policy);
        var events = Enumerable.Range(1, manifest.EventCount).Select(ordinal =>
        {
            var name = $"event-{ordinal:D6}";
            var artifact = manifest.Artifacts.Single(item => item.Name == name);
            return WorkflowExecutionCanonicalCodec.RehydrateEvent(Require(bytes, name), ContentDigest.Parse(artifact.Sha256), header);
        }).ToArray();
        var journal = WorkflowExecutionJournal.Rehydrate(header, events, workflow, policy, recordResolver);
        if (journal.Projection.HeadDigest != ContentDigest.Parse(manifest.ResultingHeadDigest) ||
            (events.Length == 0 ? header.Digest : events[^1].Digest) != journal.Projection.HeadDigest)
            throw new InvalidOperationException("Workflow execution journal replay does not reproduce the manifest head.");
        return new VerifiedResearchWorkspaceWorkflowExecutionJournal(manifest, policy, header, Array.AsReadOnly(events), journal);
    }

    private static void VerifyPredecessor(ResearchWorkspaceLocation location, ResearchWorkspaceWorkflowExecutionJournalManifest manifest)
    {
        if (manifest.PredecessorGenerationId is null)
        {
            if (manifest.PriorHeadDigest != manifest.HeaderDigest) throw new InvalidOperationException("Initial journal prior head must be the header digest.");
            return;
        }
        var relative = $"{ResearchWorkspacePaths.WorkflowExecutionJournalRoot(manifest.ExecutionId, manifest.PredecessorGenerationId)}/journal.manifest.json";
        var bytes = File.ReadAllBytes(Resolve(location, relative));
        if (ContentDigest.Sha256(bytes) != ContentDigest.Parse(manifest.PredecessorManifestSha256!))
            throw new InvalidOperationException("Workflow execution predecessor manifest digest does not reproduce.");
        var predecessor = ResearchWorkspaceWorkflowExecutionJournalManifestCodec.Rehydrate(bytes);
        if (predecessor.ExecutionId != manifest.ExecutionId || predecessor.ResultingHeadDigest != manifest.PriorHeadDigest ||
            predecessor.EventCount > manifest.EventCount)
            throw new InvalidOperationException("Workflow execution predecessor lineage is invalid.");
    }

    private static byte[] ReadArtifact(ResearchWorkspaceLocation location, ResearchWorkspaceGenerationArtifact artifact)
    {
        var bytes = File.ReadAllBytes(Resolve(location, artifact.RelativePath));
        if (ContentDigest.Sha256(bytes) != ContentDigest.Parse(artifact.Sha256))
            throw new InvalidOperationException($"Workflow execution artifact '{artifact.Name}' digest does not reproduce.");
        return bytes;
    }

    internal static string Resolve(ResearchWorkspaceLocation location, string relativePath)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(
                location.RootDirectory, relativePath, out var path))
        {
            throw new InvalidOperationException("Workflow execution path escapes the workspace.");
        }

        return path;
    }

    private static byte[] Require(IReadOnlyDictionary<string, byte[]> values, string name) =>
        values.TryGetValue(name, out var bytes) ? bytes : throw new InvalidOperationException($"Workflow execution artifact '{name}' is missing.");
}

public static class ResearchWorkspaceWorkflowExecutionTransaction
{
    public static ResearchWorkspaceWorkflowExecutionCommit Commit(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy,
        WorkflowExecutionHeader header,
        IReadOnlyList<WorkflowExecutionEvent> events,
        IWorkflowExecutionRecordResolver recordResolver,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(expectedProject);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(events);
        var journal = WorkflowExecutionJournal.Rehydrate(header, events, workflow, policy, recordResolver);
        VerifiedResearchWorkspaceWorkflowExecutionJournal? predecessor = null;
        if (expectedProject.CurrentWorkflowExecutionJournalGenerationId is not null)
        {
            predecessor = ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(location, expectedProject, workflow, recordResolver);
            if (predecessor.Journal.Projection.HeadDigest == journal.Projection.HeadDigest)
                return new ResearchWorkspaceWorkflowExecutionCommit(expectedProject, predecessor.Manifest, predecessor.Journal, AlreadyApplied: true);
            if (predecessor.Events.Count >= events.Count ||
                !predecessor.Events.Select(item => item.Digest).SequenceEqual(events.Take(predecessor.Events.Count).Select(item => item.Digest)))
                throw new ResearchWorkspaceConcurrencyException("Workflow execution journal does not extend the current persisted history.", new InvalidOperationException());
        }

        var head = journal.Projection.HeadDigest;
        var generationId = $"journal-{head.Value[..16]}-{events.Count:D6}";
        var generationRelative = ResearchWorkspacePaths.WorkflowExecutionJournalRoot(header.ExecutionId, generationId);
        var generationRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, generationRelative);
        var stagingRoot = ResearchWorkspacePaths.InProject(
            location.RootDirectory, $"{ResearchWorkspacePaths.GenerationStaging}/{generationId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        try
        {
            var records = new List<(string Name, string FileName, byte[] Bytes)>
            {
                ("authority-policy", "authority-policy.json", WorkflowExecutionCanonicalCodec.Serialize(policy)),
                ("header", "header.json", WorkflowExecutionCanonicalCodec.Serialize(header))
            };
            records.AddRange(events.Select((item, index) =>
                ($"event-{index + 1:D6}", $"event-{index + 1:D6}.json", WorkflowExecutionCanonicalCodec.Serialize(item))));
            var artifacts = records.Select(record =>
            {
                File.WriteAllBytes(Path.Combine(stagingRoot, record.FileName), record.Bytes);
                return new ResearchWorkspaceGenerationArtifact(
                    record.Name, $"{generationRelative}/{record.FileName}", ContentDigest.Sha256(record.Bytes).ToString());
            }).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();

            var manifestPath = $"{generationRelative}/journal.manifest.json";
            var placeholder = "sha256:" + new string('0', 64);
            var committedProject = expectedProject.CommitWorkflowExecutionJournalGeneration(generationId, manifestPath, placeholder);
            var manifest = new ResearchWorkspaceWorkflowExecutionJournalManifest(
                ResearchWorkspaceWorkflowExecutionJournalManifest.CurrentSchema,
                generationId, expectedProject.WorkspaceId, committedProject.Revision, header.ExecutionId,
                header.WorkflowId, header.WorkflowDigest.ToString(), header.ProtocolVersionId, header.ProtocolContentDigest.ToString(),
                policy.PolicyId, policy.Digest.ToString(), header.Digest.ToString(),
                predecessor?.Manifest.GenerationId, expectedProject.WorkflowExecutionJournalManifestSha256,
                predecessor?.Manifest.ResultingHeadDigest ?? header.Digest.ToString(), head.ToString(), events.Count, artifacts);
            var manifestBytes = ResearchWorkspaceWorkflowExecutionJournalManifestCodec.Serialize(manifest);
            var manifestDigest = ContentDigest.Sha256(manifestBytes);
            committedProject = committedProject with { WorkflowExecutionJournalManifestSha256 = manifestDigest.ToString() };
            File.WriteAllBytes(Path.Combine(stagingRoot, "journal.manifest.json"), manifestBytes);
            _ = ResearchWorkspaceWorkflowExecutionJournalManifestCodec.Rehydrate(manifestBytes);
            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterStaging);

            Directory.CreateDirectory(Path.GetDirectoryName(generationRoot)!);
            using var workspaceLock = AcquireLock(location);
            var current = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (current.Revision != expectedProject.Revision ||
                current.CurrentWorkflowExecutionJournalGenerationId != expectedProject.CurrentWorkflowExecutionJournalGenerationId ||
                current.WorkflowExecutionJournalManifestSha256 != expectedProject.WorkflowExecutionJournalManifestSha256)
                throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, current.Revision);
            if (Directory.Exists(generationRoot))
            {
                if (!GenerationMatches(stagingRoot, generationRoot))
                {
                    Quarantine(location, generationRoot, generationId);
                    Directory.Move(stagingRoot, generationRoot);
                }
            }
            else
            {
                Directory.Move(stagingRoot, generationRoot);
            }
            try
            {
                faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterPromotion);
                ResearchWorkspaceStore.WriteProject(location, committedProject);
            }
            catch
            {
                Quarantine(location, generationRoot, generationId);
                throw;
            }
            var verified = ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(location, committedProject, workflow, recordResolver);
            return new ResearchWorkspaceWorkflowExecutionCommit(committedProject, verified.Manifest, verified.Journal, AlreadyApplied: false);
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private static FileStream AcquireLock(ResearchWorkspaceLocation location)
    {
        try
        {
            return new FileStream(
                Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new ResearchWorkspaceConcurrencyException("The workspace is locked by another mutation.", exception);
        }
    }

    private static void Quarantine(ResearchWorkspaceLocation location, string generationRoot, string generationId)
    {
        var target = ResearchWorkspacePaths.InProject(
            location.RootDirectory, $"{ResearchWorkspacePaths.GenerationQuarantine}/{generationId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(generationRoot, target);
    }

    private static bool GenerationMatches(string stagedRoot, string promotedRoot)
    {
        var staged = Directory.GetFiles(stagedRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(stagedRoot, path), File.ReadAllBytes, StringComparer.Ordinal);
        var promoted = Directory.GetFiles(promotedRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(promotedRoot, path), File.ReadAllBytes, StringComparer.Ordinal);
        return staged.Count == promoted.Count && staged.All(pair =>
            promoted.TryGetValue(pair.Key, out var bytes) && pair.Value.AsSpan().SequenceEqual(bytes));
    }
}

public sealed record ResearchWorkspaceWorkflowExecutionCommit(
    ResearchWorkspaceProject Project,
    ResearchWorkspaceWorkflowExecutionJournalManifest Manifest,
    WorkflowExecutionJournal Journal,
    bool AlreadyApplied);

public sealed class ResearchWorkspaceWorkflowExecutionJournalCommitPort(
    ResearchWorkspaceLocation location,
    ResearchWorkspaceProject expectedProject,
    IWorkflowExecutionRecordResolver recordResolver) : IWorkflowExecutionJournalCommitPort
{
    public WorkflowExecutionJournalCommitResult Commit(
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy,
        WorkflowExecutionHeader header,
        IReadOnlyList<WorkflowExecutionEvent> events)
    {
        var result = ResearchWorkspaceWorkflowExecutionTransaction.Commit(
            location, expectedProject, workflow, policy, header, events, recordResolver);
        return new WorkflowExecutionJournalCommitResult(
            header.ExecutionId,
            result.Journal.Projection.HeadDigest,
            result.Journal.Events.Count,
            result.AlreadyApplied);
    }
}
