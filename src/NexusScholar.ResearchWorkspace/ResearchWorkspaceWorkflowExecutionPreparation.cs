using NexusScholar.Kernel;
using NexusScholar.Workflow;
using NexusScholar.WorkflowExecution;

namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspacePreparedWorkflowExecutionGeneration(
    ResearchWorkspaceWorkflowExecutionJournalManifest Manifest,
    string ManifestPath,
    ContentDigest ManifestDigest,
    WorkflowExecutionJournal Journal,
    VerifiedWorkflowDefinition Workflow,
    IWorkflowExecutionRecordResolver RecordResolver,
    string StagingRoot,
    string FinalRoot);

public static class ResearchWorkspaceWorkflowExecutionPreparation
{
    public static ResearchWorkspacePreparedWorkflowExecutionGeneration Prepare(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy,
        WorkflowExecutionHeader header,
        IReadOnlyList<WorkflowExecutionEvent> events,
        IWorkflowExecutionRecordResolver recordResolver)
    {
        var journal = WorkflowExecutionJournal.Rehydrate(header, events, workflow, policy, recordResolver);
        VerifiedResearchWorkspaceWorkflowExecutionJournal? predecessor = null;
        if (expectedProject.CurrentWorkflowExecutionJournalGenerationId is not null)
        {
            predecessor = ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(location, expectedProject, workflow, recordResolver);
            if (predecessor.Events.Count > events.Count ||
                !predecessor.Events.Select(item => item.Digest).SequenceEqual(events.Take(predecessor.Events.Count).Select(item => item.Digest)))
                throw new ResearchWorkspaceConcurrencyException("Workflow execution preparation does not extend current history.", new InvalidOperationException());
        }
        var generationId = $"journal-{journal.Projection.HeadDigest.Value[7..23]}-{events.Count:D6}";
        var relativeRoot = ResearchWorkspacePaths.WorkflowExecutionJournalRoot(header.ExecutionId, generationId);
        var finalRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, relativeRoot);
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory,
            $"{ResearchWorkspacePaths.GenerationStaging}/{generationId}-prepare-{Guid.NewGuid():N}");
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
                return new ResearchWorkspaceGenerationArtifact(record.Name, $"{relativeRoot}/{record.FileName}", ContentDigest.Sha256(record.Bytes).ToString());
            }).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            var manifestPath = $"{relativeRoot}/journal.manifest.json";
            var manifest = new ResearchWorkspaceWorkflowExecutionJournalManifest(
                ResearchWorkspaceWorkflowExecutionJournalManifest.CurrentSchema, generationId, expectedProject.WorkspaceId,
                checked(expectedProject.Revision + 1), header.ExecutionId, header.WorkflowId, header.WorkflowDigest.ToString(),
                header.ProtocolVersionId, header.ProtocolContentDigest.ToString(), policy.PolicyId, policy.Digest.ToString(), header.Digest.ToString(),
                predecessor?.Manifest.GenerationId, expectedProject.WorkflowExecutionJournalManifestSha256,
                predecessor?.Manifest.ResultingHeadDigest ?? header.Digest.ToString(), journal.Projection.HeadDigest.ToString(), events.Count, artifacts);
            var manifestBytes = ResearchWorkspaceWorkflowExecutionJournalManifestCodec.Serialize(manifest);
            var manifestDigest = ContentDigest.Sha256(manifestBytes);
            File.WriteAllBytes(Path.Combine(stagingRoot, "journal.manifest.json"), manifestBytes);
            return new ResearchWorkspacePreparedWorkflowExecutionGeneration(
                manifest, manifestPath, manifestDigest, journal, workflow, recordResolver, stagingRoot, finalRoot);
        }
        catch
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            throw;
        }
    }
}
