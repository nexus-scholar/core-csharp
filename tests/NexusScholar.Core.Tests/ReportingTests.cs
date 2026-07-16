using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Reporting;
using NexusScholar.Screening;
using NexusScholar.Screening.CorpusSnapshots;
using NexusScholar.Screening.FullText;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ReportingTests
{
    [TestMethod]
    public void Complete_verified_slice_conserves_and_round_trips_deterministically()
    {
        var authority = BuildAuthorities(includeFullText: true);

        var projection = ReviewFlowProjector.Project(authority, ["Local-only review."], ["No PRISMA certification claim."]);
        var report = ReviewFlowProjector.Finalize(projection);
        var second = ReviewFlowProjector.Finalize(ReviewFlowProjector.Project(authority, ["Local-only review."], ["No PRISMA certification claim."]));

        Assert.AreEqual(new ReviewFlowCounts(3, 1, 2, 1, 1, 1, 0, 1), report.Projection.Counts);
        Assert.AreEqual(1, report.Projection.TitleAbstractReasons.Single().Count);
        CollectionAssert.AreEqual(ReportingCanonicalCodec.SerializeReport(report), ReportingCanonicalCodec.SerializeReport(second));
        CollectionAssert.AreEqual(ReviewFlowMarkdownRenderer.Render(report), ReviewFlowMarkdownRenderer.Render(second));
        Assert.AreEqual(report.ReportDigest, ReportingCanonicalCodec.Rehydrate(
            ReportingCanonicalCodec.SerializeSlice(report), ReportingCanonicalCodec.SerializeReport(report), projection).ReportDigest);
    }

    [TestMethod]
    public void Missing_full_text_case_remains_a_gap_and_cannot_finalize()
    {
        var projection = ReviewFlowProjector.Project(BuildAuthorities(includeFullText: false));

        Assert.AreEqual("candidate-a", projection.Gaps.Single().CandidateId);
        var error = Assert.ThrowsExactly<ReportingRuleException>(() => ReviewFlowProjector.Finalize(projection));
        Assert.AreEqual(ReportingErrorCodes.IncompleteSlice, error.Category);
    }

    [TestMethod]
    public void Duplicate_or_nonincluded_full_text_case_fails_closed()
    {
        var source = BuildAuthorities(includeFullText: true);
        var duplicate = source with { FullTextCases = [source.FullTextCases.Single(), source.FullTextCases.Single()] };

        var error = Assert.ThrowsExactly<ReportingRuleException>(() => ReviewFlowProjector.Project(duplicate));
        Assert.AreEqual(ReportingErrorCodes.InvalidAuthority, error.Category);
    }

    [TestMethod]
    public void Codec_rejects_noncanonical_or_unsupported_narrative_bytes()
    {
        var projection = ReviewFlowProjector.Project(BuildAuthorities(includeFullText: true), nonClaims: ["No compatibility claim."]);
        var report = ReviewFlowProjector.Finalize(projection);
        var altered = ReportingCanonicalCodec.SerializeReport(report).Concat([(byte)' ']).ToArray();

        var error = Assert.ThrowsExactly<ReportingRuleException>(() => ReportingCanonicalCodec.Rehydrate(
            ReportingCanonicalCodec.SerializeSlice(report), altered, projection));
        Assert.AreEqual(ReportingErrorCodes.NonCanonicalRecord, error.Category);
    }

    [TestMethod]
    public void Finalization_requires_explicit_non_claims_and_projection_text_is_immutable()
    {
        var projection = ReviewFlowProjector.Project(BuildAuthorities(includeFullText: true), ["Local-only."], ["No certification claim."]);
        Assert.IsFalse(projection.Disclosures is string[]);
        Assert.IsFalse(projection.NonClaims is string[]);

        var missing = ReviewFlowProjector.Project(BuildAuthorities(includeFullText: true));
        var error = Assert.ThrowsExactly<ReportingRuleException>(() => ReviewFlowProjector.Finalize(missing));
        Assert.AreEqual(ReportingErrorCodes.IncompleteSlice, error.Category);
    }

    [TestMethod]
    public void Workspace_generation_drift_and_unbound_extraction_attempt_fail_closed()
    {
        var source = BuildAuthorities(includeFullText: true);
        var incompleteCut = new VerifiedReviewWorkspaceCut("workspace-reporting", 4,
            source.WorkspaceCut.Generations.Where(item => item.Role != ReviewGenerationRoles.Deduplication));
        var cutError = Assert.ThrowsExactly<ReportingRuleException>(() =>
            ReviewFlowProjector.Project(source with { WorkspaceCut = incompleteCut }));
        Assert.AreEqual(ReportingErrorCodes.InvalidAuthority, cutError.Category);

        var fullTextCase = source.FullTextCases.Single();
        var extraction = FullTextExtractionAttempt.Create(
            "unbound-extraction", fullTextCase.ArtifactChain,
            FullTextExtractionConfiguration.Create("pdf-parser", "1.0.0", FullTextExtractionRepresentations.PageText),
            Now, FullTextExtractionAttemptStatuses.Unsupported,
            failureCategory: FullTextErrorCodes.UnsupportedFileType, failureSummary: "Not admitted by this conduct policy.");
        var extractionError = Assert.ThrowsExactly<ReportingRuleException>(() => ReviewFlowProjector.Project(
            source with { FullTextCases = [fullTextCase with { ExtractionAttempt = extraction }] }));
        Assert.AreEqual(ReportingErrorCodes.InvalidAuthority, extractionError.Category);
    }

    [TestMethod]
    public void Verified_unresolved_protocol_inconsistency_blocks_final_report()
    {
        var source = BuildAuthorities(includeFullText: true);
        var value = new ProtocolDeviationRecord(
            "deviation-reporting", source.Protocol.Version.ProtocolId, source.Protocol.Version.Id, source.Protocol.Version.ContentDigest,
            "scope", null, null, null, "Observed conduct.", "Rationale.", ProtocolDeviationConstants.UnresolvedInconsistency,
            "Unknown consequence.", "Investigation pending.",
            [new ProtocolDeviationEvidenceReference("audit-note", "note-1", ContentDigest.Sha256Utf8("note"))],
            "Unresolved.", "limitations.unresolved", ActorId.From("reviewer-1"), Now, "deviation-policy", ["approval-1"],
            [new ProtocolDeviationInvalidationEffect("review-report", "report-1", ContentDigest.Sha256Utf8("report"), "block-finalization")], null);
        var digest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ProtocolDeviationConstants.SchemaId,
            ProtocolDeviationConstants.SchemaVersion, value.ToCanonicalJson()).ComputeDigest();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher("deviation-policy");
        var seed = new ProtocolSupplementalApproval("approval-1", ProtocolSupplementalTargetTypes.Deviation, value.DeviationId, digest,
            policy.PolicyId, policy.PolicyVersion, policy.Mode, ProtocolApprovalDecision.Approved, value.RecordedBy, Now, null, "Approved.", null,
            ContentDigest.Sha256Utf8("placeholder"));
        var approval = new VerifiedProtocolSupplementalApproval(new ProtocolSupplementalApproval(
            seed.ApprovalId, seed.TargetType, seed.TargetId, seed.TargetDigest, seed.PolicyId, seed.PolicyVersion, seed.PolicyMode,
            seed.Decision, seed.ApprovedBy, seed.ApprovedAt, seed.Role, seed.Rationale, seed.SupersedesApprovalId,
            seed.ToDigestEnvelope().ComputeDigest()));
        var deviation = ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(value, digest), new ReportingDeviationResolver(source.Protocol, policy, approval));
        var projection = ReviewFlowProjector.Project(source with { Deviations = [deviation] }, nonClaims: ["No final claim."]);

        Assert.IsTrue(projection.Disclosures.Contains(value.Disclosure, StringComparer.Ordinal));
        var error = Assert.ThrowsExactly<ReportingRuleException>(() => ReviewFlowProjector.Finalize(projection));
        Assert.AreEqual(ReportingErrorCodes.IncompleteSlice, error.Category);
    }

    [TestMethod]
    public void Persisted_verifier_enforces_structured_shape_conservation_and_slice_binding()
    {
        var report = ReviewFlowProjector.Finalize(ReviewFlowProjector.Project(
            BuildAuthorities(includeFullText: true), ["Local-only."], ["No certification claim."]));
        var reportBytes = ReportingCanonicalCodec.SerializeReport(report);
        var sliceBytes = ReportingCanonicalCodec.SerializeSlice(report);

        var verified = PersistedReportingVerifier.VerifyReport(reportBytes, report.ReportDigest);
        Assert.AreEqual(report.SliceDigest, verified.SliceDigest);
        Assert.AreEqual(report.SliceDigest, PersistedReportingVerifier.VerifySlice(sliceBytes, report.SliceDigest));

        var envelopeOnly = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReportingSchemas.ReportId,
            ReportingSchemas.Version, new CanonicalJsonObject().Add("fixture", "not-a-report"));
        var error = Assert.ThrowsExactly<ReportingRuleException>(() => PersistedReportingVerifier.VerifyReport(
            envelopeOnly.ToCanonicalJsonBytes(), envelopeOnly.ComputeDigest()));
        Assert.AreEqual(ReportingErrorCodes.NonCanonicalRecord, error.Category);

        var malformed = JsonNode.Parse(reportBytes)!.AsObject();
        malformed["content"]!["bindings"]!["full_text_cases"]!.AsArray()[0]!.AsObject().Remove("admission_digest");
        var malformedBytes = CanonicalBytes(malformed);
        var bindingError = Assert.ThrowsExactly<ReportingRuleException>(() =>
            PersistedReportingVerifier.VerifyReport(malformedBytes, ContentDigest.Sha256(malformedBytes)));
        Assert.AreEqual(ReportingErrorCodes.NonCanonicalRecord, bindingError.Category);
    }

    private static byte[] CanonicalBytes(JsonNode node)
    {
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(node));
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
    }

    internal static ReviewSliceAuthorities BuildAuthorities(bool includeFullText)
    {
        var protocol = BuildProtocol();
        var dedupPolicy = BuildDeduplicationPolicy();
        var dedup = BuildDeduplication(dedupPolicy.PolicyId);
        var snapshot = CorpusSnapshotService.CreateBaseline(
            "snapshot-reporting", dedup, dedupPolicy, dedupPolicy.IssuedByActorId, dedupPolicy.IssuedByRole, new FixedClock());
        var binding = ScreeningCorpusBindingAuthority.Create("binding-reporting", dedup, snapshot);
        var actor = new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer");
        var snapshotPolicy = ScreeningCorpusBindingAuthority.CreateConductPolicy(
            binding, dedup, "policy-reporting", "candidate-set-reporting", protocol,
            Criteria(protocol, ScreeningStages.TitleAbstract), 1,
            [new ScreeningConductRoleAssignment(actor.ActorId, actor.Role)], [],
            [new ScreeningExclusionReason("wrong-population", ScreeningStages.TitleAbstract)], actor, Now);
        var header = ScreeningConductHeader.Create("conduct-reporting", snapshotPolicy.Policy, actor, Now);
        var include = ScreeningConductDecision.Create(header, 1, header.Digest, "request-a", "candidate-a",
            ScreeningConductDecisionKind.Review, ScreeningVerdicts.Include, actor, "Advance.", Now);
        var exclude = ScreeningConductDecision.Create(header, 2, include.Digest, "request-c", "candidate-c",
            ScreeningConductDecisionKind.Review, ScreeningVerdicts.Exclude, actor, "Exclude.", Now,
            exclusionReasonCode: "wrong-population");
        var journal = ScreeningConductJournal.Rehydrate(header, snapshotPolicy.Policy, [include, exclude]);
        var handoff = ScreeningConductHandoff.Create("handoff-reporting", journal, Now);
        var cases = includeFullText ? new[] { BuildFullTextCase(protocol, dedup, journal, handoff, actor) } : [];
        var cut = new VerifiedReviewWorkspaceCut("workspace-reporting", 4,
        [
            new ReviewGenerationBinding(ReviewGenerationRoles.Protocol, "generation-protocol", ContentDigest.Sha256Utf8("manifest-protocol")),
            new ReviewGenerationBinding(ReviewGenerationRoles.Workflow, "generation-workflow", ContentDigest.Sha256Utf8("manifest-workflow")),
            new ReviewGenerationBinding(ReviewGenerationRoles.Deduplication, "generation-dedup", ContentDigest.Sha256Utf8("manifest-dedup")),
            new ReviewGenerationBinding(ReviewGenerationRoles.CorpusSnapshot, "generation-snapshot", ContentDigest.Sha256Utf8("manifest-snapshot")),
            new ReviewGenerationBinding(ReviewGenerationRoles.ScreeningConduct, "generation-screening", ContentDigest.Sha256Utf8("manifest-screening")),
            .. cases.Select(item => new ReviewGenerationBinding(ReviewGenerationRoles.FullText, "generation-fulltext", ContentDigest.Sha256Utf8("manifest-fulltext"), item.Admission.CandidateId))
        ]);
        var workflow = new VerifiedReportingWorkflowAuthority(
            "workflow-reporting", ContentDigest.Sha256Utf8("workflow-reporting"), protocol.Version.Id, protocol.Version.ContentDigest);
        return new ReviewSliceAuthorities(protocol, workflow, dedup, snapshot, snapshotPolicy, journal, handoff,
            cases, [], [], [], [], cut);
    }

    private static FullTextReviewCaseAuthorities BuildFullTextCase(
        VerifiedProtocolVersion protocol,
        VerifiedDeduplicationAuthorityResultDigest dedup,
        ScreeningConductJournal sourceJournal,
        ScreeningConductHandoff sourceHandoff,
        ScreeningConductActor actor)
    {
        var admission = VerifiedFullTextAdmission.Create(sourceJournal, sourceHandoff, "candidate-a");
        var bytes = System.Text.Encoding.UTF8.GetBytes("reporting full text");
        var acquisition = new FullTextAcquisitionRecord(
            "acquisition-reporting", admission.Input, FullTextAcquisitionKinds.ManualAcquisition, "local", "operator-supplied",
            new FullTextActor(actor.ActorId, FullTextActorKinds.Human), Now, FullTextAttemptStatuses.Success,
            [new FullTextSourceAttempt("attempt-reporting", "local", 1, FullTextAcquisitionKinds.ManualAcquisition,
                FullTextAttemptStatuses.Success, artifactKind: FullTextArtifactKinds.Text, mediaType: "text/plain", artifactEvidenceId: "artifact-reporting")],
            artifactEvidenceId: "artifact-reporting");
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-reporting", admission.Input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 4096);
        var chain = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(admission.Input, acquisition, artifact, bytes, 4096));
        ContentDigest.TryParse(artifact.RawByteDigest, out var rawDigest);
        var policy = FullTextScreeningConductPolicy.Create(
            "full-policy-reporting", admission.CandidateSetId,
            DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(dedup.Result)), protocol,
            Criteria(protocol, ScreeningStages.FullText), admission, 1,
            [new ScreeningConductRoleAssignment(actor.ActorId, actor.Role)], [],
            [new ScreeningExclusionReason("wrong-population-full", ScreeningStages.FullText)], actor, Now, rawDigest);
        var header = FullTextScreeningConductHeader.Create("full-conduct-reporting", policy, actor, Now);
        var decision = FullTextScreeningConductDecision.Create(
            header, 1, header.Digest, "full-request-a", admission.CandidateId, ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, actor, "Include.", Now,
            evidence: [new ScreeningConductEvidenceRef(FullTextScreeningConductEvidenceKinds.FullTextArtifact, artifact.ArtifactId, rawDigest)]);
        var journal = FullTextScreeningConductJournal.Create(policy, header);
        journal.Append(decision);
        return new FullTextReviewCaseAuthorities(admission, chain, journal, journal.CreateHandoff("full-handoff-reporting", Now));
    }

    private static VerifiedDeduplicationAuthorityResultDigest BuildDeduplication(string policyId)
    {
        var a = Candidate("candidate-a", true);
        var b = Candidate("candidate-b", true);
        var c = Candidate("candidate-c", false);
        var cluster = new DedupCluster("cluster-a-b", [a, b],
            new DedupRepresentativeResult(a.CandidateId, a.Title, a.PrimaryWorkId, a.WorkIds, [a.Source.SourceSightingId], 1d, []),
            [new DedupEvidence("evidence-a-b", DedupEvidenceKind.SourceSighting, a.CandidateId, b.CandidateId,
                "source-sighting", true, 0.99d, policyId, DeduplicationService.PolicyVersion)]);
        return DeduplicationAuthorityDigests.CreateResultDigestMaterial(new DeduplicationResult(
            "dedup-reporting", DeduplicationAuthorityDigests.ResultSchemaId, DeduplicationAuthorityDigests.ResultSchemaVersion,
            policyId, DeduplicationService.PolicyVersion, 0.95d, new Dictionary<string, int>(), [], [], [a, b, c], [cluster], [], [c], [], [], [], []));
    }

    private static VerifiedDeduplicationAuthorityPolicy BuildDeduplicationPolicy() =>
        DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
            DeduplicationAuthorityPolicyConstants.SchemaId, DeduplicationAuthorityPolicyConstants.SchemaVersion,
            DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, DeduplicationService.PolicyId, "1.0.0",
            [new DeduplicationAuthorityPolicyActorRole("alice", "owner", DeduplicationAuthorityPolicyConstants.HumanSubjectKind)],
            DeduplicationAuthorityPolicyConstants.ClosedActions.ToArray(),
            [
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, ["duplicate"]),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, ["distinct"]),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, ["uncertain"])
            ], false, "alice", "owner", Now, null, null, null));

    private static DedupCandidateRecord Candidate(string id, bool stable) => new(
        id, $"Title {id}", stable, stable ? $"doi:{id}" : null, stable ? [$"work:{id}"] : [], [],
        new DedupSightingRef("search", $"trace-{id}", $"sighting-{id}", "provider", "tool"), ["author"], 2026, null, null, ["keyword"]);

    private static VerifiedProtocolVersion BuildProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-reporting-v1", "protocol-reporting", "project-reporting", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("reporting", "report review flow"), new CanonicalJsonObject(), [], [], [],
            ContentDigest.Sha256Utf8("placeholder"), ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId, ["approval-1"], Now);
        var version = new ProtocolVersion(seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status,
            seed.Template, seed.Intent, seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers,
            seed.ToProtocolContentDigestEnvelope().ComputeDigest(), seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private static ScreeningCriteria Criteria(VerifiedProtocolVersion protocol, string stage) => new(
        $"criteria-{stage}", "1.0.0", stage, CanonicalJsonValue.From("include"), CanonicalJsonValue.From("exclude"), true,
        protocol.Version.Id, protocol.Version.ContentDigest.ToString(),
        approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
        currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class ReportingDeviationResolver(VerifiedProtocolVersion protocol, ApprovalPolicy policy,
        VerifiedProtocolSupplementalApproval approval) : IProtocolDeviationAuthorityResolver
    {
        public ApprovalPolicy ResolvePolicy(string targetType, string targetId) => policy;
        public bool IsHumanActor(ActorId actorId) => actorId == ActorId.From("reviewer-1");
        public VerifiedProtocolSupplementalApproval ResolveApproval(string approvalId) => approval;
        public VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId) => protocol;
        public VerifiedProtocolAmendment ResolveProtocolAmendment(string amendmentId) => throw new KeyNotFoundException();
    }
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
}
