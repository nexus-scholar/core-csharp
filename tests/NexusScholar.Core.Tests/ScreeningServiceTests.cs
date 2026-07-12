using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;
using NexusScholar.Search;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ScreeningServiceTests
{
    private const string ApprovedProtocolDigest = "sha256:c0cbe3ed40c4781508733a846848ab015ea7f6f95a5a4ff0c2f86907c90e9600";

    [TestMethod]
    public void Public_service_construction_requires_verified_protocol_and_deduplication_authority()
    {
        var protocol = BuildVerifiedProtocol();
        var dedup = BuildDedupResult("dedup-authority", ["candidate-1"], []) with
        {
            PolicyId = DeduplicationService.PolicyId,
            PolicyVersion = DeduplicationService.PolicyVersion
        };
        var verifiedDedup = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(dedup));
        var criteria = BuildAuthorityCriteria(protocol);

        var service = new ScreeningService(protocol, verifiedDedup, "verified-screening-set", new[] { criteria });

        Assert.IsTrue(service.CandidateSet.Locked);
        Assert.AreEqual(dedup.ResultId, service.CandidateSet.CreatedFromDedupResultId);
        Assert.AreEqual(1, service.CandidateSet.Candidates.Count);
    }

    [TestMethod]
    public void Screening_actor_cannot_be_publicly_fabricated_and_confidence_must_be_finite()
    {
        Assert.AreEqual(0, typeof(ScreeningActor).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        var candidateSet = ScreeningCandidateSet.CreateLockedReviewableCandidateSet("set", new[] { BuildCandidate("candidate", true) });
        var criteria = BuildCriteria("criteria", ScreeningStages.TitleAbstract);
        var service = new ScreeningService(candidateSet, new[] { criteria });

        foreach (var confidence in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var decision = BuildHumanDecisionWithConfidence(
                "decision-" + confidence, "set", "candidate", criteria, confidence);
            var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
            Assert.AreEqual(ScreeningErrorCodes.InvalidConfidence, error.Category);
        }
    }

    [TestMethod]
    public void Candidate_set_from_dedup_result_is_accepted_for_screening_service()
    {
        var dedup = BuildDedupResult("dedup-result-screening-001", ["candidate-1", "candidate-2"], ["candidate-3"]);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-001",
            dedup,
            locked: true);
        var service = new ScreeningService(candidateSet, []);

        Assert.AreEqual(ScreeningSchema.CandidateSetSchemaId, candidateSet.SchemaId);
        Assert.AreEqual(ScreeningSchema.CandidateSetSchemaVersion, candidateSet.SchemaVersion);
        Assert.AreEqual("screening-set-001", candidateSet.CandidateSetId);
        Assert.IsTrue(candidateSet.Locked);
        Assert.AreEqual(2, candidateSet.Candidates.Count);
        Assert.AreEqual(1, candidateSet.UnresolvedCandidates.Count);
        Assert.AreEqual("deduplication-result", candidateSet.SourceKind);
        Assert.AreEqual(dedup.ResultId, candidateSet.CreatedFromDedupResultId);
        Assert.AreEqual(0, service.Decisions.Count);
    }

    [TestMethod]
    public void Locked_reviewable_candidate_set_is_accepted_without_dedup_result()
    {
        var candidateSet = ScreeningCandidateSet.CreateLockedReviewableCandidateSet(
            "screening-set-reviewable-001",
            [BuildCandidate("candidate-reviewable-1", true)],
            sourceRefs: ["candidate-set:manual-reviewable-001"]);
        var criteria = BuildCriteria("criteria-reviewable", ScreeningStages.TitleAbstract);
        var service = new ScreeningService(candidateSet, [criteria]);
        var decision = BuildHumanDecision(
            "decision-reviewable-001",
            candidateSet.CandidateSetId,
            "candidate-reviewable-1",
            criteria.CriteriaId,
            criteria.ComputeDigest().ToString(),
            ScreeningActor.Human("human-reviewable-1"),
            ScreeningVerdicts.Include);

        service.AddDecision(decision);

        Assert.AreEqual(ScreeningSourceKinds.LockedReviewableCandidateSet, candidateSet.SourceKind);
        Assert.IsNull(candidateSet.CreatedFromDedupResultId);
        Assert.AreEqual(1, service.Decisions.Count);
    }

    [TestMethod]
    public void Raw_search_trace_cannot_be_screening_input()
    {
        var query = new SearchQueryInput("seeded screening", null, null, null, 25, 0, false, Array.Empty<string>());
        var identity = SearchCacheIdentity.Compute(query, 2026, Array.Empty<string>());
        var trace = new SearchTrace(
            "search-trace-screening-raw",
            SearchTrace.TraceSchemaId,
            SearchTrace.TraceSchemaVersion,
            new SearchTraceRequest(
                "seeded screening",
                SearchYearRange.Validate(null, null, 2026),
                null,
                25,
                0,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null),
            identity,
            Array.Empty<SearchProviderAttempt>(),
            Array.Empty<SearchProviderStat>(),
            Array.Empty<SearchSighting>(),
            new SearchSummary(0, 0, 0, 0, false),
            SearchTrace.DefaultNonClaims);

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() =>
            ScreeningService.CreateCandidateSetFromInput(trace, "screening-set-raw", false));

        Assert.AreEqual(ScreeningErrorCodes.RawSearchTraceNotScreenable, error.Category);
    }

    [TestMethod]
    public void Unlocked_candidate_set_rejects_final_human_decisions()
    {
        var dedup = BuildDedupResult("dedup-result-screening-002", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-002",
            dedup,
            locked: false);
        var criteria = new ScreeningCriteria(
            "criteria-001",
            "1.0.0",
            ScreeningStages.TitleAbstract,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            approvedProtocolBinding: "protocol-001",
            approvedProtocolDigest: ApprovedProtocolDigest,
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: ApprovedProtocolDigest);
        var service = new ScreeningService(candidateSet, [criteria]);

        var decision = new ScreeningDecision(
            "decision-001",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("human-1"),
            DateTimeOffset.UtcNow,
            "Stable title-abstract verdict.",
            0.78d,
            "criteria-001",
            criteria.ComputeDigest().ToString(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
        Assert.AreEqual(ScreeningErrorCodes.CandidateSetNotLocked, error.Category);
    }

    [TestMethod]
    public void Criteria_digest_includes_screening_stage()
    {
        var titleAbstractCriteria = BuildCriteria("criteria-stage-digest", ScreeningStages.TitleAbstract);
        var fullTextCriteria = BuildCriteria("criteria-stage-digest", ScreeningStages.FullText);

        Assert.AreNotEqual(titleAbstractCriteria.ComputeDigest(), fullTextCriteria.ComputeDigest());
    }

    [TestMethod]
    public void Human_decision_requires_actor_and_rationale()
    {
        var dedup = BuildDedupResult("dedup-result-screening-003", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-003",
            dedup,
            locked: true);
        var criteria = new ScreeningCriteria(
            "criteria-002",
            "1.0.0",
            ScreeningStages.FullText,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            approvedProtocolBinding: "protocol-screening-v1",
            approvedProtocolDigest: ApprovedProtocolDigest,
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: ApprovedProtocolDigest);
        var service = new ScreeningService(candidateSet, [criteria]);

        var missingActor = new ScreeningDecision(
            "decision-no-actor",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.FullText,
            ScreeningVerdicts.NeedsReview,
            null,
            DateTimeOffset.UtcNow,
            "Needs review based on abstract.",
            null,
            "criteria-002",
            criteria.ComputeDigest().ToString(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        var missingRationale = new ScreeningDecision(
            "decision-no-rationale",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.FullText,
            ScreeningVerdicts.NeedsReview,
            ScreeningActor.Human("human-2"),
            DateTimeOffset.UtcNow,
            null,
            null,
            "criteria-002",
            criteria.ComputeDigest().ToString(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.AreEqual(
            ScreeningErrorCodes.MissingHumanActor,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(missingActor)).Category);
        Assert.AreEqual(
            ScreeningErrorCodes.MissingRationale,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(missingRationale)).Category);
    }

    [TestMethod]
    public void Confidence_bounds_are_enforced_for_final_decisions_and_suggestions()
    {
        var dedup = BuildDedupResult("dedup-result-screening-004", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-004",
            dedup,
            locked: true);
        var criteria = new ScreeningCriteria(
            "criteria-003",
            "1.0.0",
            ScreeningStages.TitleAbstract,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            approvedProtocolBinding: "protocol-screening-v1",
            approvedProtocolDigest: ApprovedProtocolDigest,
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: ApprovedProtocolDigest);
        var service = new ScreeningService(candidateSet, [criteria]);
        var invalidConfidence = new ScreeningSuggestion(
            "suggestion-low",
            candidateSet.CandidateSetId,
            "candidate-1",
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Include,
            -0.10d,
            "Not reliable.",
            null,
            null);

        Assert.AreEqual(
            ScreeningErrorCodes.InvalidConfidence,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddSuggestion(invalidConfidence)).Category);

        var invalidDecision = new ScreeningDecision(
            "decision-bad-confidence",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("human-3"),
            DateTimeOffset.UtcNow,
            "Reject because confidence cannot be >1.",
            1.5d,
            "criteria-003",
            criteria.ComputeDigest().ToString(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.AreEqual(
            ScreeningErrorCodes.InvalidConfidence,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(invalidDecision)).Category);
    }

    [TestMethod]
    public void Automation_cannot_finalize_and_a_suggestion_cannot_be_a_decision()
    {
        var dedup = BuildDedupResult("dedup-result-screening-005", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-005",
            dedup,
            locked: true);
        var criteria = new ScreeningCriteria(
            "criteria-004",
            "1.0.0",
            ScreeningStages.TitleAbstract,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            approvedProtocolBinding: "protocol-screening-v1",
            approvedProtocolDigest: ApprovedProtocolDigest,
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: ApprovedProtocolDigest);
        var service = new ScreeningService(candidateSet, [criteria]);

        service.AddSuggestion(
            new ScreeningSuggestion(
                "suggestion-001",
                candidateSet.CandidateSetId,
                "candidate-1",
                ScreeningStages.TitleAbstract,
                ScreeningVerdicts.Include,
                0.80d,
                "LLM suggestion.",
                null,
                null));

        var aiDecision = new ScreeningDecision(
            "decision-ai-final",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Include,
            ScreeningActor.Automation("llm-worker"),
            DateTimeOffset.UtcNow,
            "Automation must not finalize.",
            0.80d,
            "criteria-004",
            criteria.ComputeDigest().ToString(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.AreEqual(
            ScreeningErrorCodes.AutomationCannotFinalize,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(aiDecision)).Category);
    }

    [TestMethod]
    public void Conflict_is_detected_from_human_disagreement_and_resolved_without_mutation()
    {
        var dedup = BuildDedupResult("dedup-result-screening-006", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-006",
            dedup,
            locked: true);
        var criteria = new ScreeningCriteria(
            "criteria-005",
            "1.0.0",
            ScreeningStages.TitleAbstract,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            approvedProtocolBinding: "protocol-screening-v1",
            approvedProtocolDigest: ApprovedProtocolDigest,
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: ApprovedProtocolDigest);
        var service = new ScreeningService(candidateSet, [criteria]);
        var digest = criteria.ComputeDigest().ToString();

        service.AddDecision(BuildHumanDecision("decision-a", candidateSet.CandidateSetId, "candidate-1", criteria.CriteriaId, digest, ScreeningActor.Human("human-a"), ScreeningVerdicts.Include));
        service.AddDecision(BuildHumanDecision("decision-b", candidateSet.CandidateSetId, "candidate-1", criteria.CriteriaId, digest, ScreeningActor.Human("human-b"), ScreeningVerdicts.Exclude));

        Assert.AreEqual(1, service.Conflicts.Count);
        var conflict = service.Conflicts[0];
        Assert.IsFalse(conflict.Resolved);
        Assert.AreEqual(2, conflict.SourceDecisionIds.Count);

        var adjudication = new ScreeningDecision(
            "decision-c",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Exclude,
            ScreeningActor.Human("chair-1"),
            DateTimeOffset.UtcNow,
            "Resolved on review.",
            1.0d,
            criteria.CriteriaId,
            digest,
            Array.Empty<string>(),
            new[] { "decision-a", "decision-b" },
            new[] { conflict.ConflictId },
            decisionKind: ScreeningDecisionKind.Adjudication,
            resolvedConflictId: conflict.ConflictId,
            nonClaims: Array.Empty<string>());

        service.AddDecision(adjudication);

        Assert.AreEqual(1, service.Conflicts.Count);
        Assert.IsTrue(service.Conflicts[0].Resolved);
        Assert.AreEqual(adjudication.DecisionId, service.Conflicts[0].ResolvedByDecisionId);
        CollectionAssert.AreEqual(
            new[] { "decision-a", "decision-b" },
            service.Decisions[^1].SourceDecisionIds.ToArray());
    }

    [TestMethod]
    public void Final_decision_rejects_invalid_protocol_binding()
    {
        var dedup = BuildDedupResult("dedup-result-screening-protocol", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-protocol",
            dedup,
            locked: true);
        var malformed = BuildCriteria(
            "criteria-bad-protocol",
            ScreeningStages.TitleAbstract,
            approvedProtocolDigest: "sha256:protocol-screening-v1");
        var service = new ScreeningService(candidateSet, [malformed]);
        var decision = BuildHumanDecision(
            "decision-bad-protocol",
            candidateSet.CandidateSetId,
            "candidate-1",
            malformed.CriteriaId,
            malformed.ComputeDigest().ToString(),
            ScreeningActor.Human("human-protocol-1"),
            ScreeningVerdicts.Include);

        Assert.AreEqual(
            ScreeningErrorCodes.CriteriaDigestMismatch,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision)).Category);

        var draft = BuildCriteria(
            "criteria-draft-protocol",
            ScreeningStages.TitleAbstract,
            approvedProtocolStatus: "draft");
        service = new ScreeningService(candidateSet, [draft]);
        decision = BuildHumanDecision(
            "decision-draft-protocol",
            candidateSet.CandidateSetId,
            "candidate-1",
            draft.CriteriaId,
            draft.ComputeDigest().ToString(),
            ScreeningActor.Human("human-protocol-1"),
            ScreeningVerdicts.Include);

        Assert.AreEqual(
            ScreeningErrorCodes.CriteriaDigestMismatch,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision)).Category);

        var wrongScope = BuildCriteria(
            "criteria-wrong-protocol-scope",
            ScreeningStages.TitleAbstract,
            approvedProtocolDigestScope: DigestScope.CanonicalJsonRecord.ToString());
        service = new ScreeningService(candidateSet, [wrongScope]);
        decision = BuildHumanDecision(
            "decision-wrong-protocol-scope",
            candidateSet.CandidateSetId,
            "candidate-1",
            wrongScope.CriteriaId,
            wrongScope.ComputeDigest().ToString(),
            ScreeningActor.Human("human-protocol-1"),
            ScreeningVerdicts.Include);

        Assert.AreEqual(
            ScreeningErrorCodes.InvalidCriteriaDigestScope,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision)).Category);

        var stale = BuildCriteria(
            "criteria-stale-protocol",
            ScreeningStages.TitleAbstract,
            currentProtocolContentDigest: ContentDigest.Sha256Utf8("different-protocol").ToString());
        service = new ScreeningService(candidateSet, [stale]);
        decision = BuildHumanDecision(
            "decision-stale-protocol",
            candidateSet.CandidateSetId,
            "candidate-1",
            stale.CriteriaId,
            stale.ComputeDigest().ToString(),
            ScreeningActor.Human("human-protocol-1"),
            ScreeningVerdicts.Include);

        Assert.AreEqual(
            ScreeningErrorCodes.CriteriaDigestMismatch,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision)).Category);
    }

    [TestMethod]
    public void Criteria_digest_mismatch_is_rejected()
    {
        var dedup = BuildDedupResult("dedup-result-screening-digest", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-digest",
            dedup,
            locked: true);
        var criteria = BuildCriteria("criteria-digest-mismatch", ScreeningStages.TitleAbstract);
        var service = new ScreeningService(candidateSet, [criteria]);
        var decision = BuildHumanDecision(
            "decision-digest-mismatch",
            candidateSet.CandidateSetId,
            "candidate-1",
            criteria.CriteriaId,
            ContentDigest.Sha256Utf8("wrong-criteria").ToString(),
            ScreeningActor.Human("human-digest-1"),
            ScreeningVerdicts.Include);

        Assert.AreEqual(
            ScreeningErrorCodes.CriteriaDigestMismatch,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision)).Category);
    }

    [TestMethod]
    public void Unknown_stage_and_verdict_are_rejected()
    {
        Assert.AreEqual(
            ScreeningErrorCodes.UnknownScreeningStage,
            Assert.ThrowsExactly<ScreeningRuleException>(() =>
                BuildCriteria("criteria-unknown-stage", "abstract_only")).Category);

        var dedup = BuildDedupResult("dedup-result-screening-vocab", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-vocab",
            dedup,
            locked: true);
        var criteria = BuildCriteria("criteria-vocab", ScreeningStages.TitleAbstract);
        var service = new ScreeningService(candidateSet, [criteria]);
        var decision = BuildHumanDecision(
            "decision-vocab",
            candidateSet.CandidateSetId,
            "candidate-1",
            criteria.CriteriaId,
            criteria.ComputeDigest().ToString(),
            ScreeningActor.Human("human-vocab-1"),
            "maybe");

        Assert.AreEqual(
            ScreeningErrorCodes.UnknownScreeningVerdict,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision)).Category);
    }

    [TestMethod]
    public void Duplicate_decision_ids_are_rejected()
    {
        var dedup = BuildDedupResult("dedup-result-screening-duplicate", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-duplicate",
            dedup,
            locked: true);
        var criteria = BuildCriteria("criteria-duplicate", ScreeningStages.TitleAbstract);
        var service = new ScreeningService(candidateSet, [criteria]);
        var digest = criteria.ComputeDigest().ToString();
        var first = BuildHumanDecision(
            "decision-duplicate",
            candidateSet.CandidateSetId,
            "candidate-1",
            criteria.CriteriaId,
            digest,
            ScreeningActor.Human("human-duplicate-1"),
            ScreeningVerdicts.Include);
        var second = BuildHumanDecision(
            "decision-duplicate",
            candidateSet.CandidateSetId,
            "candidate-1",
            criteria.CriteriaId,
            digest,
            ScreeningActor.Human("human-duplicate-2"),
            ScreeningVerdicts.Exclude);

        service.AddDecision(first);

        Assert.AreEqual(
            ScreeningErrorCodes.DuplicateDecisionId,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(second)).Category);
    }

    [TestMethod]
    public void App_conflict_rows_and_relative_full_text_file_names_are_rejected()
    {
        var dedup = BuildDedupResult("dedup-result-screening-app", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-app",
            dedup,
            locked: true);
        var titleCriteria = BuildCriteria("criteria-app", ScreeningStages.TitleAbstract);
        var fullTextCriteria = BuildCriteria("criteria-full-text", ScreeningStages.FullText);
        var service = new ScreeningService(candidateSet, [titleCriteria, fullTextCriteria]);
        var badConflictRef = new ScreeningDecision(
            "decision-app-row",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("human-app-1"),
            DateTimeOffset.UtcNow,
            "Reject app projection conflict rows.",
            null,
            titleCriteria.CriteriaId,
            titleCriteria.ComputeDigest().ToString(),
            ["project_screening_conflicts:123"],
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.AreEqual(
            ScreeningErrorCodes.AppProjectionNotCoreAuthority,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(badConflictRef)).Category);

        foreach (var appProjectionRef in new[] { "screening_batch:123", "screening_audit:456" })
        {
            var badAppProjection = new ScreeningDecision(
                $"decision-{appProjectionRef.Replace(':', '-')}",
                candidateSet.CandidateSetId,
                "candidate-1",
                null,
                null,
                ScreeningStages.TitleAbstract,
                ScreeningVerdicts.Include,
                ScreeningActor.Human("human-app-1"),
                DateTimeOffset.UtcNow,
                "Reject app projection rows.",
                null,
                titleCriteria.CriteriaId,
                titleCriteria.ComputeDigest().ToString(),
                [appProjectionRef],
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.AreEqual(
                ScreeningErrorCodes.AppProjectionNotCoreAuthority,
                Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(badAppProjection)).Category);
        }

        var badFullTextRef = new ScreeningDecision(
            "decision-fulltext-path",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.FullText,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("human-app-1"),
            DateTimeOffset.UtcNow,
            "Reject relative full-text file path.",
            null,
            fullTextCriteria.CriteriaId,
            fullTextCriteria.ComputeDigest().ToString(),
            ["fulltext-1301-v1.txt"],
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.AreEqual(
            ScreeningErrorCodes.LocalPathNotArtifactIdentity,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(badFullTextRef)).Category);

        foreach (var malformedFullTextRef in new[] { "raw-artifact-bytes:not-a-digest", "artifact:fulltext-1301-v1" })
        {
            var malformedFullTextDecision = new ScreeningDecision(
                $"decision-{malformedFullTextRef.Replace(':', '-').Replace('@', '-')}",
                candidateSet.CandidateSetId,
                "candidate-1",
                null,
                null,
                ScreeningStages.FullText,
                ScreeningVerdicts.Include,
                ScreeningActor.Human("human-app-1"),
                DateTimeOffset.UtcNow,
                "Reject non-digest full-text artifact refs.",
                null,
                fullTextCriteria.CriteriaId,
                fullTextCriteria.ComputeDigest().ToString(),
                [malformedFullTextRef],
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.AreEqual(
                ScreeningErrorCodes.FullTextArtifactRequired,
                Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(malformedFullTextDecision)).Category);
        }
    }

    [TestMethod]
    public void Resolved_conflict_is_not_resurrected_by_later_human_vote_and_downstream_handoff_can_continue()
    {
        var dedup = BuildDedupResult("dedup-result-screening-resolved", ["candidate-1"], []);
        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(
            "screening-set-resolved",
            dedup,
            locked: true);
        var titleCriteria = BuildCriteria("criteria-resolved-title", ScreeningStages.TitleAbstract);
        var fullTextCriteria = BuildCriteria("criteria-resolved-full-text", ScreeningStages.FullText);
        var service = new ScreeningService(candidateSet, [titleCriteria, fullTextCriteria]);
        var titleDigest = titleCriteria.ComputeDigest().ToString();

        service.AddDecision(BuildHumanDecision("decision-resolved-a", candidateSet.CandidateSetId, "candidate-1", titleCriteria.CriteriaId, titleDigest, ScreeningActor.Human("human-a"), ScreeningVerdicts.Include));
        service.AddDecision(BuildHumanDecision("decision-resolved-b", candidateSet.CandidateSetId, "candidate-1", titleCriteria.CriteriaId, titleDigest, ScreeningActor.Human("human-b"), ScreeningVerdicts.Exclude));
        var conflict = service.Conflicts.Single();
        var adjudication = new ScreeningDecision(
            "decision-resolved-c",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.TitleAbstract,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("chair-resolved"),
            DateTimeOffset.UtcNow,
            "Resolved after source vote review.",
            null,
            titleCriteria.CriteriaId,
            titleDigest,
            Array.Empty<string>(),
            ["decision-resolved-a", "decision-resolved-b"],
            Array.Empty<string>(),
            resolvedConflictId: conflict.ConflictId,
            decisionKind: ScreeningDecisionKind.Adjudication);
        service.AddDecision(adjudication);

        service.AddDecision(BuildHumanDecision("decision-resolved-d", candidateSet.CandidateSetId, "candidate-1", titleCriteria.CriteriaId, titleDigest, ScreeningActor.Human("human-d"), ScreeningVerdicts.Exclude));
        Assert.AreEqual(1, service.Conflicts.Count);
        Assert.IsTrue(service.Conflicts.Single().Resolved);

        var fullTextDecision = new ScreeningDecision(
            "decision-resolved-fulltext",
            candidateSet.CandidateSetId,
            "candidate-1",
            null,
            null,
            ScreeningStages.FullText,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("human-fulltext"),
            DateTimeOffset.UtcNow,
            "Allowed after prior-stage conflict resolution.",
            null,
            fullTextCriteria.CriteriaId,
            fullTextCriteria.ComputeDigest().ToString(),
            [$"raw-artifact-bytes:{ContentDigest.Sha256Utf8("full-text").ToString()}"],
            Array.Empty<string>(),
            Array.Empty<string>());
        service.AddDecision(fullTextDecision);

        Assert.AreEqual(5, service.Decisions.Count);
    }

    private static DeduplicationResult BuildDedupResult(
        string resultId,
        IReadOnlyList<string> candidateIds,
        IReadOnlyList<string> unresolvedCandidateIds)
    {
        var candidates = candidateIds.Select(id => BuildCandidate(id, true)).ToArray();
        var unresolved = unresolvedCandidateIds.Select(id => BuildCandidate(id, false)).ToArray();

        return new DeduplicationResult(
            resultId,
            "nexus.deduplication.result",
            "1.0.0",
            null,
            null,
            0.95d,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal)),
            Array.Empty<string>(),
            Array.Empty<string>(),
            candidates,
            Array.Empty<DedupCluster>(),
            Array.Empty<DedupEvidence>(),
            unresolved,
            Array.Empty<DedupReviewCandidate>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            new[] { "no-php-compatibility-claim" });
    }

    private static VerifiedProtocolVersion BuildVerifiedProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-screening-v1", "protocol-screening", "project-1", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("screening", "screen records"), new CanonicalJsonObject(),
            Array.Empty<RequiredDecisionDefinition>(), Array.Empty<ProtocolDecision>(), Array.Empty<ProtocolWaiver>(),
            ContentDigest.Sha256Utf8("placeholder"), ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId,
            new[] { "approval-1" }, DateTimeOffset.UtcNow);
        var version = new ProtocolVersion(
            seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template, seed.Intent,
            seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers,
            seed.ToProtocolContentDigestEnvelope().ComputeDigest(), seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), Array.Empty<VerifiedProtocolApproval>());
    }

    private static ScreeningCriteria BuildAuthorityCriteria(VerifiedProtocolVersion protocol) => new(
        "criteria-authority", "1.0.0", ScreeningStages.TitleAbstract,
        CanonicalJsonValue.From("include"), CanonicalJsonValue.From("exclude"), true,
        protocol.Version.Id, protocol.Version.ContentDigest.ToString(),
        approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
        currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private static ScreeningDecision BuildHumanDecisionWithConfidence(
        string decisionId, string candidateSetId, string candidateId, ScreeningCriteria criteria, double confidence) => new(
        decisionId, candidateSetId, candidateId, null, null, ScreeningStages.TitleAbstract, ScreeningVerdicts.Include,
        ScreeningActor.Human("human"), DateTimeOffset.UtcNow, "Rationale", confidence,
        criteria.CriteriaId, criteria.ComputeDigest().ToString(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    private static DedupCandidateRecord BuildCandidate(string candidateId, bool stableIdentifier)
    {
        return new DedupCandidateRecord(
            candidateId,
            "candidate title",
            stableIdentifier,
            stableIdentifier ? $"work:{candidateId}" : null,
            stableIdentifier ? new[] { $"work:{candidateId}" } : Array.Empty<string>(),
            Array.Empty<string>(),
            new DedupSightingRef("search", "trace-001", $"source-{candidateId}", "search-provider"));
    }

    private static ScreeningDecision BuildHumanDecision(
        string decisionId,
        string candidateSetId,
        string candidateId,
        string criteriaId,
        string criteriaDigest,
        ScreeningActor actor,
        string verdict)
    {
        return new ScreeningDecision(
            decisionId,
            candidateSetId,
            candidateId,
            null,
            null,
            ScreeningStages.TitleAbstract,
            verdict,
            actor,
            DateTimeOffset.UtcNow,
            "Decision rationale.",
            null,
            criteriaId,
            criteriaDigest,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ScreeningCriteria BuildCriteria(string criteriaId, string stage)
    {
        return BuildCriteria(criteriaId, stage, ApprovedProtocolDigest);
    }

    private static ScreeningCriteria BuildCriteria(
        string criteriaId,
        string stage,
        string approvedProtocolDigest = ApprovedProtocolDigest,
        string approvedProtocolDigestScope = "protocol-content",
        string approvedProtocolStatus = ScreeningProtocolBindingStatus.Approved,
        string? currentProtocolContentDigest = ApprovedProtocolDigest)
    {
        return new ScreeningCriteria(
            criteriaId,
            "1.0.0",
            stage,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            approvedProtocolBinding: "protocol-screening-v1",
            approvedProtocolDigest: approvedProtocolDigest,
            approvedProtocolDigestScope: approvedProtocolDigestScope,
            approvedProtocolStatus: approvedProtocolStatus,
            currentProtocolContentDigest: currentProtocolContentDigest);
    }
}
