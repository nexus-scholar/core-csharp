using System.Text.Json;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceReadModelBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string LockedMergeReason = "APP-01 merge-gate actions are display-only in this version.";
    private const string ParserId = "nexus.research-workspace.read-model";
    private const string ParserVersion = "1.0.0";
    private const string ImportedBy = "nexus-research-workspace-read-model";

    public static WorkspaceOverviewReadModel Build(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
        if (location is null)
        {
            return MissingWorkspace();
        }

        var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported Nexus project schema: {project.Schema}");
        }

        var verification = ResearchWorkspaceVerifier.Verify(location, project);
        _ = ResearchWorkspaceGenerationVerifier.VerifyCurrent(location, project);
        var traces = LoadImportTraces(location, project);
        var deduplicationResult = TryReadDeduplicationResult(location, project);
        var workspacePlan = TryReadWorkspacePlan(location, project);
        var reviewReportPresent = project.Outputs.TryGetValue("reviewReport", out var reviewReportPath) &&
            File.Exists(ResearchWorkspacePaths.InProject(location.RootDirectory, reviewReportPath));
        var missingGeneratedOutputs = CountMissingGeneratedOutputs(location, project);
        var analysis = BuildAnalysis(workspacePlan, deduplicationResult, reviewReportPresent);
        var attention = BuildAttention(verification, missingGeneratedOutputs);
        var state = StateFor(project, verification, workspacePlan, missingGeneratedOutputs);
        var lockedActions = BuildLockedActions(workspacePlan);
        var candidateContext = CandidateContext.Create(deduplicationResult);
        var evidenceRecords = BuildEvidenceRows(traces, candidateContext);
        var candidateSummaries = BuildCandidateSummaries(candidateContext);
        var reviewQueue = BuildReviewQueue(deduplicationResult, candidateContext, lockedActions);
        var candidateDetails = BuildCandidateDetails(deduplicationResult, candidateContext, lockedActions);

        return new WorkspaceOverviewReadModel(
            state,
            project.Title,
            project.WorkspaceId,
            ProjectLocationLabel(location, workingDirectory),
            project.NonClaims,
            BuildVerification(verification),
            analysis,
            attention,
            BuildWorkflowSteps(state),
            BuildImports(project, traces),
            evidenceRecords,
            reviewQueue,
            BuildClusterSummaries(deduplicationResult),
            candidateSummaries,
            candidateDetails,
            lockedActions);
    }

    private static WorkspaceOverviewReadModel MissingWorkspace()
    {
        return new WorkspaceOverviewReadModel(
            WorkspaceState.Missing,
            null,
            null,
            "missing",
            Array.Empty<string>(),
            new VerificationHealthReadModel(0, 0, 0, 0, 0, 0, 0, 0, IsValid: false),
            new AnalysisSummaryReadModel(
                DeduplicationResultPresent: false,
                WorkspacePlanPresent: false,
                ReviewReportPresent: false,
                ExactDuplicateClusterCount: 0,
                ReviewRequiredCandidateCount: 0,
                BlockingMergeGateCount: 0),
            new[]
            {
                new WorkspaceAttentionItem(
                    "missing-workspace",
                    BlockSeverity.Blocking,
                    "No Nexus research workspace found in the current folder or its parents.",
                    null)
            },
            BuildWorkflowSteps(WorkspaceState.Missing),
            Array.Empty<ImportSourceSummary>(),
            Array.Empty<EvidenceRecordRow>(),
            Array.Empty<ReviewQueueItem>(),
            Array.Empty<DuplicateClusterSummary>(),
            Array.Empty<DuplicateCandidateSummary>(),
            Array.Empty<DuplicateCandidateDetail>(),
            Array.Empty<LockedDecisionAction>());
    }

    private static IReadOnlyList<SearchImportTrace> LoadImportTraces(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        var traces = new List<SearchImportTrace>();
        foreach (var input in project.Inputs.Where(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal)))
        {
            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, input.EffectiveRelativePath, out var sourcePath) ||
                !File.Exists(sourcePath))
            {
                continue;
            }

            var sourceBytes = File.ReadAllBytes(sourcePath);
            var digest = ContentDigest.Sha256(sourceBytes).ToString();
            if (!string.Equals(digest, input.Sha256, StringComparison.Ordinal))
            {
                continue;
            }

            var source = SearchImportAliases.NormalizeSource(input.Source);
            var format = SearchImportAliases.NormalizeFormat(input.Format);
            traces.Add(new SearchImportService().Parse(
                $"{input.EffectiveInputId}.import-trace",
                new SearchImportRequest(
                    source,
                    SearchImportAliases.ParserFormatFor(format),
                    ParserId,
                    ParserVersion,
                    ImportedBy,
                    project.CreatedAt,
                    input.QueryText),
                sourceBytes));
        }

        return traces
            .OrderBy(trace => trace.TraceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static DeduplicationResult? TryReadDeduplicationResult(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        var relativePath = OutputPath(project, "deduplicationResult", ResearchWorkspacePaths.CurrentDeduplicationResult);
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var path) ||
            !File.Exists(path))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<DeduplicationResult>(File.ReadAllText(path), JsonOptions);
        return result ?? throw new JsonException($"Deduplication result did not contain an object: {relativePath}");
    }

    private static WorkspacePlan? TryReadWorkspacePlan(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        var relativePath = OutputPath(project, "workspacePlan", ResearchWorkspacePaths.CurrentWorkspacePlan);
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var path) ||
            !File.Exists(path))
        {
            return null;
        }

        var plan = JsonSerializer.Deserialize<WorkspacePlan>(File.ReadAllText(path), UiContractJson.SerializerOptions);
        return plan ?? throw new JsonException($"Workspace plan did not contain an object: {relativePath}");
    }

    private static string OutputPath(ResearchWorkspaceProject project, string key, string fallback)
    {
        return project.Outputs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static int CountMissingGeneratedOutputs(ResearchWorkspaceLocation location, ResearchWorkspaceProject project)
    {
        var missing = 0;
        foreach (var relativePath in project.Outputs.Values)
        {
            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var outputPath) ||
                !File.Exists(outputPath))
            {
                missing++;
            }
        }

        return missing;
    }

    private static WorkspaceState StateFor(
        ResearchWorkspaceProject project,
        ResearchWorkspaceVerificationReport verification,
        WorkspacePlan? workspacePlan,
        int missingGeneratedOutputs)
    {
        if (verification.DigestMismatches.Count > 0 ||
            verification.MissingFiles.Count > 0 ||
            verification.MissingImportTraces.Count > 0 ||
            verification.InvalidPaths.Count > 0 ||
            missingGeneratedOutputs > 0)
        {
            return WorkspaceState.NeedsAttention;
        }

        if (project.Inputs.Count(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal)) == 0)
        {
            return WorkspaceState.Initialized;
        }

        if (workspacePlan is not null)
        {
            return HasReviewWork(workspacePlan) ? WorkspaceState.ReviewReady : WorkspaceState.Analyzed;
        }

        return verification.ParserWarningCount > 0 || verification.SkippedRecordCount > 0
            ? WorkspaceState.ImportedWithWarnings
            : WorkspaceState.Imported;
    }

    private static bool HasReviewWork(WorkspacePlan plan)
    {
        return plan.Mode == BlockMode.Review ||
            plan.Blocks.Any(block =>
                block.Severity is BlockSeverity.ReviewRequired or BlockSeverity.Blocking or BlockSeverity.Critical ||
                string.Equals(block.Kind, KnownBlockKinds.HumanGateMergeDecision, StringComparison.Ordinal));
    }

    private static string ProjectLocationLabel(ResearchWorkspaceLocation location, string workingDirectory)
    {
        var root = Path.GetFullPath(location.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(root, current, StringComparison.OrdinalIgnoreCase)
            ? "current folder"
            : "parent workspace";
    }

    private static VerificationHealthReadModel BuildVerification(ResearchWorkspaceVerificationReport report)
    {
        return new VerificationHealthReadModel(
            report.InputCount,
            report.FilesUnchanged,
            report.MissingFiles.Count,
            report.DigestMismatches.Count,
            report.InvalidPaths.Count,
            report.MissingImportTraces.Count,
            report.ParserWarningCount,
            report.SkippedRecordCount,
            report.IsValid);
    }

    private static AnalysisSummaryReadModel BuildAnalysis(
        WorkspacePlan? workspacePlan,
        DeduplicationResult? deduplicationResult,
        bool reviewReportPresent)
    {
        return new AnalysisSummaryReadModel(
            deduplicationResult is not null,
            workspacePlan is not null,
            reviewReportPresent,
            deduplicationResult?.Clusters.Count ?? 0,
            deduplicationResult?.ReviewRequiredCandidates.Count ?? 0,
            workspacePlan?.Blocks.Count(block => string.Equals(block.Kind, KnownBlockKinds.HumanGateMergeDecision, StringComparison.Ordinal)) ?? 0);
    }

    private static IReadOnlyList<WorkspaceAttentionItem> BuildAttention(
        ResearchWorkspaceVerificationReport report,
        int missingGeneratedOutputs)
    {
        var items = new List<WorkspaceAttentionItem>();
        AddPathItems(items, "missing-file", BlockSeverity.Blocking, "Input file is missing.", report.MissingFiles);
        AddPathItems(items, "digest-mismatch", BlockSeverity.Blocking, "Input digest changed.", report.DigestMismatches);
        AddPathItems(items, "invalid-path", BlockSeverity.Blocking, "Workspace-relative path is invalid.", report.InvalidPaths);
        AddPathItems(items, "missing-import-trace", BlockSeverity.Warning, "Import trace is missing.", report.MissingImportTraces);
        if (missingGeneratedOutputs > 0)
        {
            items.Add(new WorkspaceAttentionItem(
                "missing-generated-output",
                BlockSeverity.Warning,
                $"{missingGeneratedOutputs} generated output file(s) are missing.",
                null));
        }

        return items.ToArray();
    }

    private static void AddPathItems(
        ICollection<WorkspaceAttentionItem> items,
        string code,
        BlockSeverity severity,
        string message,
        IEnumerable<string> targets)
    {
        foreach (var target in targets.OrderBy(value => value, StringComparer.Ordinal))
        {
            items.Add(new WorkspaceAttentionItem(code, severity, message, target));
        }
    }

    private static IReadOnlyList<WorkflowStepReadModel> BuildWorkflowSteps(WorkspaceState state)
    {
        return new[]
        {
            Step("init", "Initialize workspace", IsAtLeast(state, WorkspaceState.Initialized), state == WorkspaceState.Missing, "nexus init"),
            Step("import", "Import local Search exports", IsAtLeast(state, WorkspaceState.Imported), state == WorkspaceState.Initialized, "nexus import search"),
            Step("verify", "Verify local files", IsAtLeast(state, WorkspaceState.Imported), state is WorkspaceState.Imported or WorkspaceState.ImportedWithWarnings, "nexus verify"),
            Step("analyze", "Analyze local evidence", IsAtLeast(state, WorkspaceState.Analyzed), state is WorkspaceState.Imported or WorkspaceState.ImportedWithWarnings, "nexus analyze"),
            Step("review", "Review candidate records", state == WorkspaceState.ReviewReady, state == WorkspaceState.ReviewReady, "nexus review"),
            Step("clusters", "Inspect duplicate clusters", state is WorkspaceState.Analyzed or WorkspaceState.ReviewReady, state == WorkspaceState.Analyzed, "nexus clusters")
        };
    }

    private static WorkflowStepReadModel Step(
        string stepId,
        string label,
        bool complete,
        bool current,
        string nextCommand)
    {
        var status = complete ? "complete" : current ? "current" : "pending";
        return new WorkflowStepReadModel(stepId, label, status, current ? nextCommand : null);
    }

    private static bool IsAtLeast(WorkspaceState state, WorkspaceState minimum)
    {
        return Rank(state) >= Rank(minimum);
    }

    private static int Rank(WorkspaceState state) => state switch
    {
        WorkspaceState.Missing => 0,
        WorkspaceState.Initialized => 1,
        WorkspaceState.Imported => 2,
        WorkspaceState.ImportedWithWarnings => 2,
        WorkspaceState.Analyzed => 3,
        WorkspaceState.ReviewReady => 4,
        WorkspaceState.NeedsAttention => 1,
        _ => 0
    };

    private static IReadOnlyList<ImportSourceSummary> BuildImports(
        ResearchWorkspaceProject project,
        IReadOnlyList<SearchImportTrace> traces)
    {
        var tracesByInputId = traces.ToDictionary(
            trace => trace.TraceId.EndsWith(".import-trace", StringComparison.Ordinal)
                ? trace.TraceId[..^".import-trace".Length]
                : trace.TraceId,
            StringComparer.Ordinal);

        return project.Inputs
            .Where(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal))
            .OrderBy(input => input.EffectiveInputId, StringComparer.Ordinal)
            .Select(input =>
            {
                tracesByInputId.TryGetValue(input.EffectiveInputId, out var trace);
                return new ImportSourceSummary(
                    input.EffectiveInputId,
                    input.Source,
                    input.Format,
                    input.EffectiveRelativePath,
                    input.ImportTracePath,
                    trace?.Metadata.SourceFileDigest,
                    trace?.Metadata.RecordCount ?? 0,
                    trace?.ImportedRecords.Count ?? 0,
                    trace?.ParserWarnings.Count ?? 0,
                    trace?.ImportedRecords.Count(record => record.IsSkipped) ?? 0);
            })
            .ToArray();
    }

    private static IReadOnlyList<EvidenceRecordRow> BuildEvidenceRows(
        IReadOnlyList<SearchImportTrace> traces,
        CandidateContext context)
    {
        return traces
            .SelectMany(trace => trace.ImportedRecords
                .OrderBy(record => record.SourceRecordId, StringComparer.Ordinal)
                .Select(record => BuildEvidenceRow(trace, record, context)))
            .OrderBy(row => row.SourceTraceId, StringComparer.Ordinal)
            .ThenBy(row => row.SourceRecordId, StringComparer.Ordinal)
            .ToArray();
    }

    private static EvidenceRecordRow BuildEvidenceRow(
        SearchImportTrace trace,
        SearchImportRecord record,
        CandidateContext context)
    {
        context.CandidatesBySource.TryGetValue(SourceKey(trace.TraceId, record.SourceRecordId), out var candidate);
        var candidateId = candidate?.CandidateId;
        var clusterId = candidateId is not null && context.ClusterIdByCandidateId.TryGetValue(candidateId, out var cluster)
            ? cluster
            : null;
        var duplicateState = DuplicateState(candidateId, clusterId, context.ReviewCandidateIds);
        var warningCount = record.Notices.Count +
            trace.ParserWarnings.Count(notice => string.Equals(notice.SourceRecordId, record.SourceRecordId, StringComparison.Ordinal));

        return new EvidenceRecordRow(
            record.Work.Title,
            string.Join("; ", record.Authors),
            record.Year,
            record.Venue,
            record.SourceDatabaseOrTool,
            record.Work.PrimaryWorkId?.ToString() ?? record.SourceIdentifier ?? record.SourceIdentifiers.FirstOrDefault(),
            warningCount,
            duplicateState,
            TraceInputId(trace.TraceId),
            record.SourceRecordId,
            trace.TraceId,
            trace.Metadata.SourceFileDigest,
            record.RawRecordDigest,
            candidateId,
            clusterId);
    }

    private static string DuplicateState(string? candidateId, string? clusterId, ISet<string> reviewCandidateIds)
    {
        if (clusterId is not null)
        {
            return "exact-cluster";
        }

        if (candidateId is not null && reviewCandidateIds.Contains(candidateId))
        {
            return "review-required";
        }

        return candidateId is null ? "none" : "candidate";
    }

    private static IReadOnlyList<DuplicateClusterSummary> BuildClusterSummaries(DeduplicationResult? result)
    {
        return result?.Clusters
            .OrderBy(cluster => cluster.ClusterId, StringComparer.Ordinal)
            .Select(cluster => new DuplicateClusterSummary(
                cluster.ClusterId,
                cluster.Representative.Title,
                cluster.Representative.PrimaryWorkId,
                cluster.Members.Count,
                cluster.Evidence.Count,
                cluster.Evidence.Any(evidence => evidence.ReviewRequired)))
            .ToArray() ?? Array.Empty<DuplicateClusterSummary>();
    }

    private static IReadOnlyList<DuplicateCandidateSummary> BuildCandidateSummaries(CandidateContext context)
    {
        return context.CandidatesById.Values
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(candidate =>
            {
                var clusterId = context.ClusterIdByCandidateId.GetValueOrDefault(candidate.CandidateId);
                return ToCandidateSummary(candidate, DuplicateState(candidate.CandidateId, clusterId, context.ReviewCandidateIds), clusterId);
            })
            .ToArray();
    }

    private static IReadOnlyList<ReviewQueueItem> BuildReviewQueue(
        DeduplicationResult? result,
        CandidateContext context,
        IReadOnlyList<LockedDecisionAction> lockedActions)
    {
        return result?.ReviewRequiredCandidates
            .OrderBy(candidate => candidate.CandidateAId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.CandidateBId, StringComparer.Ordinal)
            .Select(candidate =>
            {
                context.CandidatesById.TryGetValue(candidate.CandidateAId, out var candidateA);
                context.CandidatesById.TryGetValue(candidate.CandidateBId, out var candidateB);
                var pairId = PairId(candidate.CandidateAId, candidate.CandidateBId);
                return new ReviewQueueItem(
                    pairId,
                    candidate.CandidateAId,
                    candidate.CandidateBId,
                    $"{candidateA?.Title ?? candidate.CandidateAId} / {candidateB?.Title ?? candidate.CandidateBId}",
                    candidate.TitleSimilarity,
                    candidate.ThresholdUsed,
                    ActionsForPair(lockedActions, candidate.CandidateAId, candidate.CandidateBId));
            })
            .ToArray() ?? Array.Empty<ReviewQueueItem>();
    }

    private static IReadOnlyList<DuplicateCandidateDetail> BuildCandidateDetails(
        DeduplicationResult? result,
        CandidateContext context,
        IReadOnlyList<LockedDecisionAction> lockedActions)
    {
        if (result is null)
        {
            return Array.Empty<DuplicateCandidateDetail>();
        }

        return result.ReviewRequiredCandidates
            .OrderBy(candidate => candidate.CandidateAId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.CandidateBId, StringComparer.Ordinal)
            .Where(candidate =>
                context.CandidatesById.ContainsKey(candidate.CandidateAId) &&
                context.CandidatesById.ContainsKey(candidate.CandidateBId))
            .Select(candidate =>
            {
                var candidateA = context.CandidatesById[candidate.CandidateAId];
                var candidateB = context.CandidatesById[candidate.CandidateBId];
                var clusterA = context.ClusterIdByCandidateId.GetValueOrDefault(candidateA.CandidateId);
                var clusterB = context.ClusterIdByCandidateId.GetValueOrDefault(candidateB.CandidateId);
                return new DuplicateCandidateDetail(
                    PairId(candidate.CandidateAId, candidate.CandidateBId),
                    ToCandidateSummary(candidateA, DuplicateState(candidateA.CandidateId, clusterA, context.ReviewCandidateIds), clusterA),
                    ToCandidateSummary(candidateB, DuplicateState(candidateB.CandidateId, clusterB, context.ReviewCandidateIds), clusterB),
                    candidate.TitleSimilarity,
                    candidate.ThresholdUsed,
                    EvidenceForPair(result, candidate),
                    ActionsForPair(lockedActions, candidate.CandidateAId, candidate.CandidateBId));
            })
            .ToArray();
    }

    private static IReadOnlyList<EvidenceRefReadModel> EvidenceForPair(DeduplicationResult result, DedupReviewCandidate candidate)
    {
        return result.Evidence
            .Where(evidence =>
                string.Equals(evidence.SubjectCandidateId, candidate.CandidateAId, StringComparison.Ordinal) &&
                string.Equals(evidence.ObjectCandidateId, candidate.CandidateBId, StringComparison.Ordinal) ||
                string.Equals(evidence.SubjectCandidateId, candidate.CandidateBId, StringComparison.Ordinal) &&
                string.Equals(evidence.ObjectCandidateId, candidate.CandidateAId, StringComparison.Ordinal))
            .OrderBy(evidence => evidence.EvidenceId, StringComparer.Ordinal)
            .Select(evidence => new EvidenceRefReadModel(
                evidence.Kind.ToString(),
                evidence.EvidenceId,
                evidence.Reason,
                null,
                evidence.ReviewRequired ? "review-required" : null))
            .ToArray();
    }

    private static IReadOnlyList<LockedDecisionAction> BuildLockedActions(WorkspacePlan? plan)
    {
        return plan?.Blocks
            .Where(block => string.Equals(block.Kind, KnownBlockKinds.HumanGateMergeDecision, StringComparison.Ordinal))
            .OrderBy(block => block.BlockId, StringComparer.Ordinal)
            .SelectMany(block => block.Actions.Select(action => new LockedDecisionAction(
                action.ActionId,
                action.Label.EndsWith("locked", StringComparison.OrdinalIgnoreCase)
                    ? action.Label
                    : $"{action.Label} - locked",
                action.Kind,
                action.TargetRef,
                action.CommandKind,
                IsExecutable: false,
                LockedMergeReason)))
            .ToArray() ?? Array.Empty<LockedDecisionAction>();
    }

    private static IReadOnlyList<LockedDecisionAction> ActionsForPair(
        IReadOnlyList<LockedDecisionAction> actions,
        string candidateAId,
        string candidateBId)
    {
        var pair = PairId(candidateAId, candidateBId);
        return actions
            .Where(action => string.Equals(action.TargetRef, pair, StringComparison.Ordinal))
            .ToArray();
    }

    private static DuplicateCandidateSummary ToCandidateSummary(
        DedupCandidateRecord candidate,
        string duplicateState,
        string? clusterId)
    {
        return new DuplicateCandidateSummary(
            candidate.CandidateId,
            candidate.Title,
            candidate.PrimaryWorkId,
            candidate.Source.SourceTraceId,
            candidate.Source.SourceRecordId ?? string.Empty,
            duplicateState,
            clusterId);
    }

    private static string PairId(string candidateAId, string candidateBId) => $"{candidateAId}|{candidateBId}";

    private static string SourceKey(string traceId, string? sourceRecordId) => $"{traceId}|{sourceRecordId ?? string.Empty}";

    private static string TraceInputId(string traceId) =>
        traceId.EndsWith(".import-trace", StringComparison.Ordinal)
            ? traceId[..^".import-trace".Length]
            : traceId;

    private sealed record CandidateContext(
        IReadOnlyDictionary<string, DedupCandidateRecord> CandidatesById,
        IReadOnlyDictionary<string, DedupCandidateRecord> CandidatesBySource,
        IReadOnlyDictionary<string, string> ClusterIdByCandidateId,
        ISet<string> ReviewCandidateIds)
    {
        public static CandidateContext Create(DeduplicationResult? result)
        {
            if (result is null)
            {
                return new CandidateContext(
                    new Dictionary<string, DedupCandidateRecord>(StringComparer.Ordinal),
                    new Dictionary<string, DedupCandidateRecord>(StringComparer.Ordinal),
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
            }

            var byId = result.RawCandidates
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var bySource = result.RawCandidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Source.SourceRecordId))
                .GroupBy(candidate => SourceKey(candidate.Source.SourceTraceId, candidate.Source.SourceRecordId), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var clusterByCandidate = result.Clusters
                .SelectMany(cluster => cluster.Members.Select(member => new { member.CandidateId, cluster.ClusterId }))
                .GroupBy(item => item.CandidateId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().ClusterId, StringComparer.Ordinal);
            var reviewIds = result.ReviewRequiredCandidates
                .SelectMany(candidate => new[] { candidate.CandidateAId, candidate.CandidateBId })
                .ToHashSet(StringComparer.Ordinal);

            return new CandidateContext(byId, bySource, clusterByCandidate, reviewIds);
        }
    }
}
