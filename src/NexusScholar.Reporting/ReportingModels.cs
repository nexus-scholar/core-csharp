using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Screening;
using NexusScholar.Screening.CorpusSnapshots;
using NexusScholar.Screening.FullText;
using NexusScholar.Workflow;

namespace NexusScholar.Reporting;

public static class ReportingSchemas
{
    public const string SliceBindingId = "nexus.reporting.review-slice-binding";
    public const string ReportId = "nexus.reporting.review-flow-report";
    public const string Version = "1.0.0";
}

public static class ReviewGenerationRoles
{
    public const string Protocol = "protocol";
    public const string Workflow = "workflow";
    public const string Deduplication = "deduplication";
    public const string CorpusSnapshot = "corpus-snapshot";
    public const string ScreeningConduct = "screening-conduct";
    public const string FullText = "full-text";

    public static IReadOnlyList<string> RequiredSingletons { get; } =
        [Protocol, Workflow, Deduplication, CorpusSnapshot, ScreeningConduct];
}

public static class ReportingErrorCodes
{
    public const string InvalidAuthority = "invalid-reporting-authority";
    public const string IncompleteSlice = "incomplete-reporting-slice";
    public const string ConservationFailure = "reporting-conservation-failure";
    public const string NonCanonicalRecord = "non-canonical-reporting-record";
}

public sealed class ReportingRuleException : InvalidOperationException
{
    public ReportingRuleException(string category, string message) : base(message) => Category = category;
    public string Category { get; }
}

public sealed record ReviewGenerationBinding(string Role, string GenerationId, ContentDigest ManifestDigest, string? CandidateId = null);

public sealed class VerifiedReviewWorkspaceCut
{
    internal VerifiedReviewWorkspaceCut(string workspaceId, long projectRevision, IEnumerable<ReviewGenerationBinding> generations)
    {
        WorkspaceId = Require(workspaceId, nameof(workspaceId));
        if (projectRevision < 0) throw new ReportingRuleException(ReportingErrorCodes.InvalidAuthority, "Project revision cannot be negative.");
        ProjectRevision = projectRevision;
        var ordered = (generations ?? throw new ArgumentNullException(nameof(generations)))
            .Select(item => item ?? throw new ReportingRuleException(ReportingErrorCodes.InvalidAuthority, "Generation binding cannot be null."))
            .OrderBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0 || ordered.Any(item => string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.GenerationId) || !item.ManifestDigest.IsValid))
            throw new ReportingRuleException(ReportingErrorCodes.InvalidAuthority, "Workspace cut requires complete generation bindings.");
        if (ordered.Select(item => (item.Role, item.CandidateId)).Distinct().Count() != ordered.Length)
            throw new ReportingRuleException(ReportingErrorCodes.InvalidAuthority, "Workspace generation roles must be unique per candidate.");
        Generations = Array.AsReadOnly(ordered);
        Digest = Envelope().ComputeDigest();
    }

    public string WorkspaceId { get; }
    public long ProjectRevision { get; }
    public IReadOnlyList<ReviewGenerationBinding> Generations { get; }
    public ContentDigest Digest { get; }

    internal DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, ReportingSchemas.SliceBindingId, ReportingSchemas.Version,
        new CanonicalJsonObject().Add("workspace_id", WorkspaceId).Add("project_revision", ProjectRevision)
            .Add("generations", CanonicalJsonValue.Array(Generations.Select(item =>
            {
                var value = new CanonicalJsonObject().Add("role", item.Role).Add("generation_id", item.GenerationId)
                    .Add("manifest_digest", item.ManifestDigest.ToString());
                if (item.CandidateId is not null) value.Add("candidate_id", item.CandidateId);
                return value;
            }).ToArray())));

    private static string Require(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Value is required.", name);
}

public sealed record FullTextReviewCaseAuthorities(
    VerifiedFullTextAdmission Admission,
    VerifiedFullTextChain ArtifactChain,
    FullTextScreeningConductJournal Journal,
    FullTextScreeningConductHandoff Handoff,
    FullTextExtractionAttempt? ExtractionAttempt = null);

public sealed class VerifiedReportingWorkflowAuthority
{
    internal VerifiedReportingWorkflowAuthority(
        string workflowId,
        ContentDigest workflowDigest,
        string protocolVersionId,
        ContentDigest protocolContentDigest,
        IEnumerable<ReportingSupplementalBinding>? waiverBindings = null,
        IEnumerable<ReportingSupplementalBinding>? amendmentBindings = null,
        string? templateId = null,
        string? templateVersion = null,
        ContentDigest? templateDigest = null)
    {
        WorkflowId = !string.IsNullOrWhiteSpace(workflowId) ? workflowId : throw new ArgumentException("Workflow id is required.", nameof(workflowId));
        WorkflowDigest = workflowDigest.IsValid ? workflowDigest : throw new ArgumentException("Workflow digest is required.", nameof(workflowDigest));
        ProtocolVersionId = !string.IsNullOrWhiteSpace(protocolVersionId) ? protocolVersionId : throw new ArgumentException("Protocol version id is required.", nameof(protocolVersionId));
        ProtocolContentDigest = protocolContentDigest.IsValid ? protocolContentDigest : throw new ArgumentException("Protocol content digest is required.", nameof(protocolContentDigest));
        WaiverBindings = SnapshotBindings(waiverBindings);
        AmendmentBindings = SnapshotBindings(amendmentBindings);
        TemplateId = templateId;
        TemplateVersion = templateVersion;
        TemplateDigest = templateDigest;
    }

    public string WorkflowId { get; }
    public ContentDigest WorkflowDigest { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public IReadOnlyList<ReportingSupplementalBinding> WaiverBindings { get; }
    public IReadOnlyList<ReportingSupplementalBinding> AmendmentBindings { get; }
    public string? TemplateId { get; }
    public string? TemplateVersion { get; }
    public ContentDigest? TemplateDigest { get; }

    public static VerifiedReportingWorkflowAuthority FromVerified(VerifiedWorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return new VerifiedReportingWorkflowAuthority(
            workflow.Definition.WorkflowId,
            workflow.Definition.WorkflowDigest,
            workflow.Definition.ProtocolVersionId,
            workflow.Definition.ProtocolContentDigest,
            workflow.Definition.ResolvedInputBindings.Where(item => item.WaiverId is not null)
                .Select(item => new ReportingSupplementalBinding(item.WaiverId!, item.SourceDigest)),
            workflow.Definition.ResolvedInputBindings.Where(item => item.AmendmentId is not null)
                .Select(item => new ReportingSupplementalBinding(item.AmendmentId!, item.SourceDigest))
                .Concat(workflow.Definition.InvalidationPlanEntries.Select(item =>
                    new ReportingSupplementalBinding(item.AmendmentId, item.AmendmentSourceDigest))),
            workflow.Definition.TemplateId, workflow.Definition.TemplateVersion, workflow.Definition.TemplateDigest);
    }

    private static IReadOnlyList<ReportingSupplementalBinding> SnapshotBindings(IEnumerable<ReportingSupplementalBinding>? values)
    {
        var result = (values ?? Array.Empty<ReportingSupplementalBinding>()).Distinct().OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        if (result.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != result.Length)
            throw new ReportingRuleException(ReportingErrorCodes.InvalidAuthority, "Workflow supplemental authority bindings must be unique.");
        return Array.AsReadOnly(result);
    }
}

public sealed record ReportingSupplementalBinding(string Id, ContentDigest Digest);

public sealed record ReviewSliceAuthorities(
    VerifiedProtocolVersion Protocol,
    VerifiedReportingWorkflowAuthority Workflow,
    VerifiedDeduplicationAuthorityResultDigest Deduplication,
    VerifiedCorpusSnapshot Snapshot,
    VerifiedSnapshotBoundScreeningPolicy ScreeningPolicy,
    ScreeningConductJournal ScreeningJournal,
    ScreeningConductHandoff ScreeningHandoff,
    IReadOnlyList<FullTextReviewCaseAuthorities> FullTextCases,
    IReadOnlyList<VerifiedProtocolWaiver> Waivers,
    IReadOnlyList<VerifiedProtocolAmendment> Amendments,
    IReadOnlyList<ResearchEvent> ProvenanceEvents,
    IReadOnlyList<VerifiedProtocolDeviation> Deviations,
    VerifiedReviewWorkspaceCut WorkspaceCut,
    VerifiedRapidReviewProfile? RapidReviewProfile = null);

public sealed record ReviewFlowCounts(
    int Identified, int DuplicatesConsolidated, int PostDedup,
    int TitleAbstractIncluded, int TitleAbstractExcluded,
    int FullTextIncluded, int FullTextExcluded, int Included);

public sealed record ReviewReasonCount(string Code, int Count);
public sealed record ReviewFlowGap(string Category, string CandidateId, string Detail);
public sealed record ReviewAuditCounts(int Conflicts, int Adjudications, int Corrections, int Invalidations);

public sealed class ReviewFlowProjection
{
    internal ReviewFlowProjection(ReviewSliceAuthorities authorities, ReviewFlowCounts counts,
        IReadOnlyList<ReviewReasonCount> titleAbstractReasons, IReadOnlyList<ReviewReasonCount> fullTextReasons,
        ReviewAuditCounts audit, IReadOnlyList<ReviewFlowGap> gaps, IReadOnlyList<string> disclosures, IReadOnlyList<string> nonClaims)
    {
        Authorities = authorities with
        {
            FullTextCases = Array.AsReadOnly(authorities.FullTextCases.ToArray()),
            Waivers = Array.AsReadOnly(authorities.Waivers.ToArray()),
            Amendments = Array.AsReadOnly(authorities.Amendments.ToArray()),
            ProvenanceEvents = Array.AsReadOnly(authorities.ProvenanceEvents.ToArray()),
            Deviations = Array.AsReadOnly(authorities.Deviations.ToArray())
        };
        Counts = counts;
        TitleAbstractReasons = Array.AsReadOnly(titleAbstractReasons.ToArray());
        FullTextReasons = Array.AsReadOnly(fullTextReasons.ToArray());
        Audit = audit; Gaps = Array.AsReadOnly(gaps.ToArray());
        Disclosures = Array.AsReadOnly(disclosures.ToArray());
        NonClaims = Array.AsReadOnly(nonClaims.ToArray());
    }
    internal ReviewSliceAuthorities Authorities { get; }
    public ReviewFlowCounts Counts { get; }
    public IReadOnlyList<ReviewReasonCount> TitleAbstractReasons { get; }
    public IReadOnlyList<ReviewReasonCount> FullTextReasons { get; }
    public ReviewAuditCounts Audit { get; }
    public IReadOnlyList<ReviewFlowGap> Gaps { get; }
    public IReadOnlyList<string> Disclosures { get; }
    public IReadOnlyList<string> NonClaims { get; }
}

public sealed class VerifiedReviewFlowReport
{
    internal VerifiedReviewFlowReport(ReviewFlowProjection projection, DigestEnvelope sliceEnvelope, DigestEnvelope reportEnvelope)
    {
        Projection = projection; SliceEnvelope = sliceEnvelope; ReportEnvelope = reportEnvelope;
    }
    public ReviewFlowProjection Projection { get; }
    public DigestEnvelope SliceEnvelope { get; }
    public ContentDigest SliceDigest => SliceEnvelope.ComputeDigest();
    public DigestEnvelope ReportEnvelope { get; }
    public ContentDigest ReportDigest => ReportEnvelope.ComputeDigest();
    public VerifiedReviewWorkspaceCut WorkspaceCut => Projection.Authorities.WorkspaceCut;
}
