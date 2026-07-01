using System.Globalization;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.AppServices;

public sealed record SearchDedupWorkspacePlanInput(
    string WorkspaceId,
    string Title,
    SearchImportTrace ImportTrace,
    DeduplicationResult DeduplicationResult,
    string? Description = null);

public sealed class SearchDedupWorkspacePlanComposer
{
    private const string ProjectionSummary = "Read-only app projection from Search import and Deduplication evidence. Not Core authority.";

    public WorkspacePlan Compose(SearchDedupWorkspacePlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.ImportTrace, nameof(input.ImportTrace));
        ArgumentNullException.ThrowIfNull(input.DeduplicationResult, nameof(input.DeduplicationResult));

        var workspaceId = NotBlank(input.WorkspaceId, nameof(input.WorkspaceId));
        var title = NotBlank(input.Title, nameof(input.Title));
        var warningGroups = CollectWarningGroups(input.ImportTrace).ToArray();
        var reviewCandidates = input.DeduplicationResult.ReviewRequiredCandidates
            .OrderBy(candidate => candidate.CandidateAId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.CandidateBId, StringComparer.Ordinal)
            .ToArray();
        var mode = warningGroups.Length > 0 || reviewCandidates.Length > 0
            ? BlockMode.Review
            : BlockMode.Audit;

        var blocks = new List<ResearchBlockDescriptor>
        {
            ComposeImportSummaryBlock(input.ImportTrace, mode, warningGroups)
        };

        blocks.AddRange(warningGroups.Select((group, index) => ComposeWarningSummaryBlock(input.ImportTrace, group, mode, index)));

        var clusters = input.DeduplicationResult.Clusters
            .OrderBy(cluster => cluster.ClusterId, StringComparer.Ordinal)
            .ToArray();
        blocks.AddRange(clusters.Select(cluster => ComposeClusterBlock(input.DeduplicationResult, cluster, mode)));

        var candidatesById = input.DeduplicationResult.RawCandidates
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        blocks.AddRange(reviewCandidates.Select(candidate => ComposeComparisonBlock(input.DeduplicationResult, candidatesById, candidate)));
        blocks.AddRange(reviewCandidates.Select(candidate => ComposeMergeGateBlock(input.DeduplicationResult, candidate)));

        return new WorkspacePlan(
            workspaceId,
            title,
            mode,
            blocks,
            string.IsNullOrWhiteSpace(input.Description) ? ProjectionSummary : input.Description.Trim(),
            ComposeContextRefs(input.ImportTrace, input.DeduplicationResult));
    }

    private static ResearchBlockDescriptor ComposeImportSummaryBlock(
        SearchImportTrace trace,
        BlockMode mode,
        IReadOnlyList<WarningGroup> warningGroups)
    {
        var skippedCount = trace.ImportedRecords.Count(record => record.IsSkipped);
        var resolvedCount = trace.ImportedRecords.Count(record => record.IsResolved);
        var severity = warningGroups.Count == 0 && skippedCount == 0 ? BlockSeverity.Success : BlockSeverity.Warning;
        var payload = new CanonicalJsonObject()
            .Add("schema_id", trace.SchemaId)
            .Add("schema_version", trace.SchemaVersion)
            .Add("trace_id", trace.TraceId)
            .Add("source_database_or_tool", trace.Metadata.SourceDatabaseOrTool)
            .Add("export_format", trace.Metadata.ExportFormat)
            .Add("record_count", trace.Metadata.RecordCount)
            .Add("imported_record_count", trace.ImportedRecords.Count)
            .Add("resolved_record_count", resolvedCount)
            .Add("skipped_record_count", skippedCount)
            .Add("sighting_count", trace.Sightings.Count)
            .Add("warning_category_count", warningGroups.Count)
            .Add("parser_warning_count", trace.ParserWarnings.Count)
            .Add("source_file_digest", trace.Metadata.SourceFileDigest)
            .Add("source_file_digest_scope", trace.Metadata.SourceFileDigestScope)
            .Add("network_calls", false)
            .Add("live_providers", false)
            .Add("projection_authority", "app-projection-only");

        return new ResearchBlockDescriptor(
            "block.import.summary",
            KnownBlockKinds.ImportSummary,
            "Search import summary",
            mode,
            severity,
            BlockSourceKind.AppProjection,
            ComposeImportEvidenceRefs(trace),
            warningGroups.Count == 0
                ? Array.Empty<ValidationRef>()
                : new[]
                {
                    new ValidationRef(
                        "import-warnings-present",
                        BlockSeverity.Warning,
                        "Imported records include parser warnings or record notices.",
                        trace.TraceId)
                },
            new[] { ShowDetailsAction("show-import-summary", "Show import summary", trace.TraceId) },
            trace.TraceSummary(),
            SerializePayload(payload));
    }

    private static ResearchBlockDescriptor ComposeWarningSummaryBlock(
        SearchImportTrace trace,
        WarningGroup group,
        BlockMode mode,
        int index)
    {
        var severity = group.RequiresReview ? BlockSeverity.ReviewRequired : BlockSeverity.Warning;
        var payload = new CanonicalJsonObject()
            .Add("category", group.Category)
            .Add("warning_count", group.Items.Count)
            .Add("requires_review", group.RequiresReview)
            .Add("messages", ToArray(group.Items.Select(item => item.Message).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)))
            .Add("source_record_ids", ToArray(group.Items.Select(item => item.SourceRecordId).WhereNotBlank().Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)))
            .Add("record_indexes", ToArray(group.Items.Select(item => item.RecordIndex).Where(value => value.HasValue).Select(value => value!.Value).OrderBy(value => value)))
            .Add("raw_record_digests", ToArray(group.Items.Select(item => item.RawRecordDigest).WhereNotBlank().Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)))
            .Add("projection_authority", "app-projection-only");

        return new ResearchBlockDescriptor(
            string.Create(
                CultureInfo.InvariantCulture,
                $"block.import.warning.{index + 1:000}.{StableSegment(group.Category)}"),
            KnownBlockKinds.ImportWarningSummary,
            $"Import warning: {group.Category}",
            mode,
            severity,
            BlockSourceKind.AppProjection,
            ComposeImportEvidenceRefs(trace).Concat(group.Items.SelectMany(WarningEvidenceRefs)).DistinctByKey().ToArray(),
            new[]
            {
                new ValidationRef(
                    group.Category,
                    severity,
                    "Imported evidence requires review before later workflow steps rely on it.",
                    trace.TraceId)
            },
            new[] { ShowDetailsAction($"show-warning-{StableSegment(group.Category)}", "Show warning records", trace.TraceId) },
            $"{group.Items.Count} imported warning record(s) grouped by category.",
            SerializePayload(payload));
    }

    private static ResearchBlockDescriptor ComposeClusterBlock(
        DeduplicationResult result,
        DedupCluster cluster,
        BlockMode mode)
    {
        var reviewRequired = cluster.Evidence.Any(evidence => evidence.ReviewRequired);
        var payload = new CanonicalJsonObject()
            .Add("result_id", result.ResultId)
            .Add("cluster_id", cluster.ClusterId)
            .Add("member_count", cluster.Members.Count)
            .Add("member_ids", ToArray(cluster.Members.Select(member => member.CandidateId).OrderBy(value => value, StringComparer.Ordinal)))
            .Add("representative_candidate_id", cluster.Representative.CandidateId)
            .Add("representative_title", cluster.Representative.Title)
            .Add("representative_work_ids", ToArray(cluster.Representative.WorkIds.OrderBy(value => value, StringComparer.Ordinal)))
            .Add("evidence_ids", ToArray(cluster.Evidence.Select(evidence => evidence.EvidenceId).OrderBy(value => value, StringComparer.Ordinal)))
            .Add("evidence_kinds", ToArray(cluster.Evidence.Select(evidence => evidence.Kind.ToString()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)))
            .Add("exact_identifier_evidence_count", cluster.Evidence.Count(evidence => evidence.Kind == DedupEvidenceKind.ExactIdentifier))
            .Add("review_required", reviewRequired)
            .Add("projection_authority", "app-projection-only");

        AddOptional(payload, "representative_primary_work_id", cluster.Representative.PrimaryWorkId);

        return new ResearchBlockDescriptor(
            $"block.dedup.cluster.{StableSegment(cluster.ClusterId)}",
            KnownBlockKinds.DedupCandidateCluster,
            $"Exact duplicate cluster {cluster.ClusterId}",
            mode,
            reviewRequired ? BlockSeverity.ReviewRequired : BlockSeverity.Success,
            BlockSourceKind.AppProjection,
            new[] { DedupResultEvidenceRef(result) }.Concat(cluster.Members.SelectMany(CandidateEvidenceRefs)).DistinctByKey().ToArray(),
            Array.Empty<ValidationRef>(),
            new[] { ShowDetailsAction($"show-cluster-{StableSegment(cluster.ClusterId)}", "Show duplicate cluster", cluster.ClusterId) },
            $"{cluster.Members.Count} candidate record(s) grouped for deduplication review.",
            SerializePayload(payload));
    }

    private static ResearchBlockDescriptor ComposeComparisonBlock(
        DeduplicationResult result,
        IReadOnlyDictionary<string, DedupCandidateRecord> candidatesById,
        DedupReviewCandidate candidate)
    {
        candidatesById.TryGetValue(candidate.CandidateAId, out var candidateA);
        candidatesById.TryGetValue(candidate.CandidateBId, out var candidateB);

        var payload = new CanonicalJsonObject()
            .Add("result_id", result.ResultId)
            .Add("candidate_a_id", candidate.CandidateAId)
            .Add("candidate_b_id", candidate.CandidateBId)
            .Add("title_similarity", candidate.TitleSimilarity)
            .Add("threshold_used", candidate.ThresholdUsed)
            .Add("candidate_a", CandidatePayload(candidate.CandidateAId, candidateA))
            .Add("candidate_b", CandidatePayload(candidate.CandidateBId, candidateB))
            .Add("review_reason", "title-similarity-threshold")
            .Add("projection_authority", "app-projection-only");

        var pairSegment = PairSegment(candidate);
        return new ResearchBlockDescriptor(
            $"block.dedup.comparison.{pairSegment}",
            KnownBlockKinds.DedupRecordComparison,
            $"Review possible duplicate: {candidate.CandidateAId} / {candidate.CandidateBId}",
            BlockMode.Review,
            BlockSeverity.ReviewRequired,
            BlockSourceKind.AppProjection,
            new[] { DedupResultEvidenceRef(result) }.Concat(CandidateEvidenceRefs(candidateA)).Concat(CandidateEvidenceRefs(candidateB)).DistinctByKey().ToArray(),
            new[]
            {
                new ValidationRef(
                    "dedup-review-required",
                    BlockSeverity.ReviewRequired,
                    "Candidate pair requires human review before merge.",
                    $"{candidate.CandidateAId}|{candidate.CandidateBId}")
            },
            new[] { ShowDetailsAction($"show-comparison-{pairSegment}", "Show record comparison", $"{candidate.CandidateAId}|{candidate.CandidateBId}") },
            "Title similarity reached the review threshold and needs a human merge decision.",
            SerializePayload(payload));
    }

    private static ResearchBlockDescriptor ComposeMergeGateBlock(
        DeduplicationResult result,
        DedupReviewCandidate candidate)
    {
        var pairSegment = PairSegment(candidate);
        var pairRef = $"{candidate.CandidateAId}|{candidate.CandidateBId}";
        var payload = new CanonicalJsonObject()
            .Add("result_id", result.ResultId)
            .Add("candidate_a_id", candidate.CandidateAId)
            .Add("candidate_b_id", candidate.CandidateBId)
            .Add("human_authority_required", true)
            .Add("decision_required", true)
            .Add("command_execution", false)
            .Add("projection_authority", "app-projection-only");

        return new ResearchBlockDescriptor(
            $"block.dedup.merge-gate.{pairSegment}",
            KnownBlockKinds.HumanGateMergeDecision,
            $"Human merge decision required: {candidate.CandidateAId} / {candidate.CandidateBId}",
            BlockMode.Review,
            BlockSeverity.Blocking,
            BlockSourceKind.AppProjection,
            new[] { DedupResultEvidenceRef(result) },
            new[]
            {
                new ValidationRef(
                    "human-merge-decision-required",
                    BlockSeverity.Blocking,
                    "Automation cannot decide this merge.",
                    pairRef)
            },
            new[]
            {
                PlaceholderAction($"accept-merge-{pairSegment}", BlockActionKind.AcceptMerge, "Accept merge", pairRef),
                PlaceholderAction($"reject-merge-{pairSegment}", BlockActionKind.RejectMerge, "Reject merge", pairRef),
                PlaceholderAction($"mark-unresolved-{pairSegment}", BlockActionKind.MarkUnresolved, "Mark unresolved", pairRef)
            },
            "Human confirmation is required; no command is executed by this projection.",
            SerializePayload(payload));
    }

    private static IEnumerable<WarningGroup> CollectWarningGroups(SearchImportTrace trace)
    {
        var items = new List<WarningItem>();
        var parserWarnings = trace.ParserWarnings.Count > 0
            ? trace.ParserWarnings
            : trace.Metadata.ParserWarnings;

        items.AddRange(parserWarnings.Select(notice => new WarningItem(
            notice.Category,
            notice.Message,
            notice.SourceRecordId,
            notice.RecordIndex,
            null)));

        foreach (var record in trace.ImportedRecords)
        {
            if (record.IsSkipped)
            {
                items.Add(new WarningItem(
                    SearchImportErrorCodes.SkippedRecord,
                    record.SkipReason ?? "Imported record was skipped.",
                    record.SourceRecordId,
                    null,
                    record.RawRecordDigest));
            }

            items.AddRange(record.Notices.Select(notice => new WarningItem(
                notice.Category,
                notice.Message,
                notice.SourceRecordId ?? record.SourceRecordId,
                notice.RecordIndex,
                record.RawRecordDigest)));
        }

        return items
            .GroupBy(item => item.Category, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new WarningGroup(group.Key, group.OrderBy(item => item.SourceRecordId, StringComparer.Ordinal).ThenBy(item => item.RecordIndex).ThenBy(item => item.Message, StringComparer.Ordinal).ToArray()));
    }

    private static IReadOnlyList<EvidenceRef> ComposeContextRefs(SearchImportTrace trace, DeduplicationResult result)
    {
        return ComposeImportEvidenceRefs(trace)
            .Concat(new[] { DedupResultEvidenceRef(result) })
            .DistinctByKey()
            .ToArray();
    }

    private static IReadOnlyList<EvidenceRef> ComposeImportEvidenceRefs(SearchImportTrace trace)
    {
        var refs = new List<EvidenceRef>
        {
            new(
                KnownEvidenceRefKinds.ImportSource,
                trace.TraceId,
                "Search import trace",
                trace.Metadata.SourceFileDigest,
                trace.Metadata.SourceFileDigestScope)
        };

        if (!string.IsNullOrWhiteSpace(trace.Metadata.SourceFileDigest))
        {
            refs.Add(new EvidenceRef(
                KnownEvidenceRefKinds.SourceFileDigest,
                trace.Metadata.SourceFileDigest,
                "Source file digest",
                trace.Metadata.SourceFileDigest,
                trace.Metadata.SourceFileDigestScope));
        }

        return refs;
    }

    private static EvidenceRef DedupResultEvidenceRef(DeduplicationResult result) =>
        new(KnownEvidenceRefKinds.DeduplicationResult, result.ResultId, "Deduplication result");

    private static IEnumerable<EvidenceRef> WarningEvidenceRefs(WarningItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.SourceRecordId))
        {
            yield return new EvidenceRef(KnownEvidenceRefKinds.ImportRecord, item.SourceRecordId, "Import record");
        }

        if (!string.IsNullOrWhiteSpace(item.RawRecordDigest))
        {
            yield return new EvidenceRef(
                KnownEvidenceRefKinds.RawRecordDigest,
                item.RawRecordDigest,
                "Raw record digest",
                item.RawRecordDigest,
                "raw-record-bytes");
        }
    }

    private static IEnumerable<EvidenceRef> CandidateEvidenceRefs(DedupCandidateRecord? candidate)
    {
        if (candidate is null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Source.SourceRecordId))
        {
            yield return new EvidenceRef(KnownEvidenceRefKinds.ImportRecord, candidate.Source.SourceRecordId, "Import record");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Source.SourceFileDigest))
        {
            yield return new EvidenceRef(
                KnownEvidenceRefKinds.SourceFileDigest,
                candidate.Source.SourceFileDigest,
                "Source file digest",
                candidate.Source.SourceFileDigest,
                candidate.Source.SourceFileDigestScope);
        }

        if (!string.IsNullOrWhiteSpace(candidate.Source.RawRecordDigest))
        {
            yield return new EvidenceRef(
                KnownEvidenceRefKinds.RawRecordDigest,
                candidate.Source.RawRecordDigest,
                "Raw record digest",
                candidate.Source.RawRecordDigest,
                "raw-record-bytes");
        }
    }

    private static CanonicalJsonObject CandidatePayload(string candidateId, DedupCandidateRecord? candidate)
    {
        var payload = new CanonicalJsonObject()
            .Add("candidate_id", candidateId)
            .Add("title", candidate?.Title ?? string.Empty)
            .Add("has_stable_identifier", candidate?.HasStableIdentifier ?? false)
            .Add("work_ids", ToArray(candidate?.WorkIds ?? Array.Empty<string>()))
            .Add("source_specific_ids", ToArray(candidate?.SourceSpecificIds ?? Array.Empty<string>()));

        AddOptional(payload, "primary_work_id", candidate?.PrimaryWorkId);
        AddOptional(payload, "source_trace_id", candidate?.Source.SourceTraceId);
        AddOptional(payload, "source_sighting_id", candidate?.Source.SourceSightingId);
        AddOptional(payload, "source_record_id", candidate?.Source.SourceRecordId);

        return payload;
    }

    private static BlockActionDescriptor ShowDetailsAction(string actionId, string label, string targetRef) =>
        new(actionId, BlockActionKind.ShowDetails, label, requiresHumanConfirmation: false, isDestructive: false, targetRef: targetRef);

    private static BlockActionDescriptor PlaceholderAction(string actionId, BlockActionKind kind, string label, string targetRef) =>
        new(actionId, kind, label, requiresHumanConfirmation: true, isDestructive: false, targetRef: targetRef);

    private static void AddOptional(CanonicalJsonObject payload, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            payload.Add(name, value);
        }
    }

    private static string SerializePayload(CanonicalJsonObject payload) =>
        CanonicalJsonSerializer.Serialize(payload);

    private static CanonicalJsonArray ToArray(IEnumerable<string> values) =>
        CanonicalJsonValue.Array(values.Select(CanonicalJsonValue.From).ToArray());

    private static CanonicalJsonArray ToArray(IEnumerable<int> values) =>
        CanonicalJsonValue.Array(values.Select(CanonicalJsonValue.From).ToArray());

    private static string PairSegment(DedupReviewCandidate candidate) =>
        $"{StableSegment(candidate.CandidateAId)}.{StableSegment(candidate.CandidateBId)}";

    private static string StableSegment(string value)
    {
        var trimmed = NotBlank(value, nameof(value)).ToLowerInvariant();
        var buffer = new char[trimmed.Length];
        var length = 0;
        var previousSeparator = false;
        foreach (var character in trimmed)
        {
            var next = char.IsAsciiLetterOrDigit(character) ? character : '-';
            if (next == '-' && previousSeparator)
            {
                continue;
            }

            buffer[length++] = next;
            previousSeparator = next == '-';
        }

        var result = new string(buffer, 0, length).Trim('-');
        return result.Length == 0 ? "value" : result;
    }

    private static string NotBlank(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be blank.", parameterName);
        }

        return value.Trim();
    }

    private sealed record WarningItem(
        string Category,
        string Message,
        string? SourceRecordId,
        int? RecordIndex,
        string? RawRecordDigest);

    private sealed record WarningGroup(string Category, IReadOnlyList<WarningItem> Items)
    {
        public bool RequiresReview =>
            Items.Any(item =>
                string.Equals(item.Category, SearchImportErrorCodes.MissingRequiredField, StringComparison.Ordinal) ||
                string.Equals(item.Category, SearchImportErrorCodes.MalformedRecord, StringComparison.Ordinal) ||
                string.Equals(item.Category, SearchImportErrorCodes.SkippedRecord, StringComparison.Ordinal));
    }
}

internal static class SearchDedupWorkspacePlanComposerEnumerableExtensions
{
    public static IEnumerable<string> WhereNotBlank(this IEnumerable<string?> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim());

    public static IEnumerable<EvidenceRef> DistinctByKey(this IEnumerable<EvidenceRef> refs) =>
        refs
            .GroupBy(reference => $"{reference.Kind}|{reference.Value}|{reference.Digest}|{reference.Scope}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(reference => reference.Kind, StringComparer.Ordinal)
            .ThenBy(reference => reference.Value, StringComparer.Ordinal)
            .ThenBy(reference => reference.Digest, StringComparer.Ordinal)
            .ThenBy(reference => reference.Scope, StringComparer.Ordinal);
}
