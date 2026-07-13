using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Screening;
using NexusScholar.Search;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ScreeningFixtureTests
{
    private const string ApprovedProtocolDigest = "sha256:c0cbe3ed40c4781508733a846848ab015ea7f6f95a5a4ff0c2f86907c90e9600";

    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "fixtures", "screening");

    private static readonly string[] ExpectedFixtureFiles =
    {
        "screening-input-dedup-result-candidates.json",
        "screening-input-locked-candidate-set.json",
        "screening-input-raw-search-trace-rejected.json",
        "screening-criteria-canonical-digest.json",
        "screening-criteria-key-order-stable.json",
        "screening-criteria-stage-specific.json",
        "screening-human-include-decision.json",
        "screening-human-exclude-decision.json",
        "screening-human-needs-review-decision.json",
        "screening-human-missing-actor-negative.json",
        "screening-human-missing-rationale-negative.json",
        "screening-confidence-bounds-negative.json",
        "screening-ai-suggestion-not-final.json",
        "screening-conflict-created-from-disagreement.json",
        "screening-conflict-resolved-by-human.json",
        "screening-unresolved-conflict-blocks-handoff.json",
        "screening-adjudication-source-decision-links.json",
        "screening-app-assignment-projection-not-authority.json",
        "screening-cli-file-output-not-core-authority.json"
    };

    [TestMethod]
    public void Screening_fixture_files_are_present()
    {
        Directory.CreateDirectory(FixtureDirectory);
        var files = Directory.EnumerateFiles(FixtureDirectory, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedFile in ExpectedFixtureFiles)
        {
            Assert.IsTrue(files.Contains(expectedFile), $"Missing fixture '{expectedFile}'.");
        }
    }

    [TestMethod]
    public void Screening_fixture_files_have_local_contract_metadata()
    {
        foreach (var expectedFile in ExpectedFixtureFiles)
        {
            using var document = LoadFixture(expectedFile);
            var root = document.RootElement;

            Assert.AreEqual(Path.GetFileNameWithoutExtension(expectedFile), root.GetProperty("fixtureId").GetString());
            Assert.AreEqual("local-gate-9-screening-implementation", root.GetProperty("sourceKind").GetString());
            Assert.AreEqual("local-gate-9-screening-local", root.GetProperty("sourceCommit").GetString());
            Assert.IsTrue(root.GetProperty("sourceRefs").GetArrayLength() > 0);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(item => item.GetString() == "no-broad-php-screening-compatibility"));
            Assert.IsTrue(ContentDigest.TryParse(root.GetProperty("inputDigest").GetString(), out _));
            Assert.IsTrue(ContentDigest.TryParse(root.GetProperty("outputDigest").GetString(), out _));
        }
    }

    [TestMethod]
    public void Screening_input_dedup_result_candidates_is_accepted()
    {
        using var document = LoadFixture("screening-input-dedup-result-candidates.json");
        var @case = document.RootElement.GetProperty("case");
        var expected = @case.GetProperty("expected");

        var candidateSet = BuildCandidateSet(@case.GetProperty("candidateSet"), true);
        var service = new ScreeningService(candidateSet, []);

        Assert.AreEqual(expected.GetProperty("sourceKind").GetString(), candidateSet.SourceKind);
        Assert.AreEqual(expected.GetProperty("locked").GetBoolean(), candidateSet.Locked);
        Assert.AreEqual(expected.GetProperty("candidateCount").GetInt32(), candidateSet.Candidates.Count);
        Assert.AreEqual(expected.GetProperty("unresolvedCandidateCount").GetInt32(), candidateSet.UnresolvedCandidates.Count);
        Assert.AreEqual(0, service.Decisions.Count);
    }

    [TestMethod]
    public void Screening_input_locked_candidate_set_allows_final_decisions()
    {
        using var document = LoadFixture("screening-input-locked-candidate-set.json");
        var @case = document.RootElement.GetProperty("case");
        var criteria = BuildCriteria(@case.GetProperty("criteria"));
        var expected = @case.GetProperty("expected");

        var criteriaList = new[] { criteria };

        var lockedSet = BuildCandidateSet(@case.GetProperty("lockedCandidateSet"), true);
        var unlockedSet = BuildCandidateSet(@case.GetProperty("unlockedCandidateSet"), false);

        var lockedService = new ScreeningService(lockedSet, criteriaList);
        var unlockedService = new ScreeningService(unlockedSet, criteriaList);
        var lockedDecision = BuildDecision(@case.GetProperty("decision"), criteria.CriteriaId, criteria.ComputeDigest().ToString(), lockedSet.CandidateSetId);
        var unlockedDecision = BuildDecision(@case.GetProperty("decision"), criteria.CriteriaId, criteria.ComputeDigest().ToString(), unlockedSet.CandidateSetId);

        lockedService.AddDecision(lockedDecision);
        Assert.AreEqual(1, lockedService.Decisions.Count);
        Assert.AreEqual(ScreeningSourceKinds.LockedReviewableCandidateSet, lockedSet.SourceKind);
        Assert.IsNull(lockedSet.CreatedFromDedupResultId);

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => unlockedService.AddDecision(unlockedDecision));
        Assert.AreEqual(expected.GetProperty("unlockedDecisionCategory").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_raw_search_trace_is_rejected_as_input()
    {
        using var document = LoadFixture("screening-input-raw-search-trace-rejected.json");
        var @case = document.RootElement.GetProperty("case");
        var expected = @case.GetProperty("expected");
        var trace = BuildSearchTrace(@case.GetProperty("trace"));

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() =>
            ScreeningService.CreateCandidateSetFromInput(trace, "screening-set-invalid", false));

        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_criteria_digest_is_canonical_and_order_stable()
    {
        using var document = LoadFixture("screening-criteria-canonical-digest.json");
        var @case = document.RootElement.GetProperty("case");

        var first = BuildCriteria(@case.GetProperty("criteriaA"));
        var second = BuildCriteria(@case.GetProperty("criteriaB"));

        Assert.AreEqual(first.ComputeDigest(), second.ComputeDigest());
        Assert.AreEqual(first.CriteriaVersion, second.CriteriaVersion);
        Assert.AreEqual(first.CriteriaId, second.CriteriaId);
    }

    [TestMethod]
    public void Screening_criteria_key_order_is_stable()
    {
        using var document = LoadFixture("screening-criteria-key-order-stable.json");
        var @case = document.RootElement.GetProperty("case");

        var first = BuildCriteria(@case.GetProperty("criteriaA"));
        var second = BuildCriteria(@case.GetProperty("criteriaB"));

        Assert.AreEqual(first.ComputeDigest(), second.ComputeDigest());
    }

    [TestMethod]
    public void Screening_criteria_are_stage_bound()
    {
        using var document = LoadFixture("screening-criteria-stage-specific.json");
        var @case = document.RootElement.GetProperty("case");
        var criteria = BuildCriteria(@case.GetProperty("criteria"));
        var candidateSet = BuildCandidateSet(@case.GetProperty("candidateSet"), true);
        var service = new ScreeningService(candidateSet, [criteria]);

        var matchingDecision = BuildDecision(
            @case.GetProperty("matchingDecision"),
            criteria.CriteriaId,
            criteria.ComputeDigest().ToString(),
            candidateSet.CandidateSetId);

        service.AddDecision(matchingDecision);
        Assert.AreEqual(1, service.Decisions.Count);

        var mismatchedDecision = BuildDecision(
            @case.GetProperty("mismatchedDecision"),
            criteria.CriteriaId,
            criteria.ComputeDigest().ToString(),
            candidateSet.CandidateSetId,
            allowAppend: false);

        Assert.AreEqual(
            ScreeningErrorCodes.InvalidScreeningInput,
            Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(mismatchedDecision)).Category);
    }

    [TestMethod]
    public void Screening_human_include_decision_is_accepted()
    {
        using var document = LoadFixture("screening-human-include-decision.json");
        ValidateHumanDecision(document.RootElement.GetProperty("case"));
    }

    [TestMethod]
    public void Screening_human_exclude_decision_is_accepted()
    {
        using var document = LoadFixture("screening-human-exclude-decision.json");
        ValidateHumanDecision(document.RootElement.GetProperty("case"));
    }

    [TestMethod]
    public void Screening_human_needs_review_decision_is_accepted()
    {
        using var document = LoadFixture("screening-human-needs-review-decision.json");
        ValidateHumanDecision(document.RootElement.GetProperty("case"));
    }

    [TestMethod]
    public void Screening_human_decision_requires_actor()
    {
        using var document = LoadFixture("screening-human-missing-actor-negative.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCase(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var decision = BuildDecision(@case.GetProperty("decision"), criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId, omitActor: true);
        var expected = @case.GetProperty("expected");

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_human_decision_requires_rationale()
    {
        using var document = LoadFixture("screening-human-missing-rationale-negative.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCase(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var decision = BuildDecision(@case.GetProperty("decision"), criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId, omitRationale: true);
        var expected = @case.GetProperty("expected");

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_confidence_bounds_are_enforced()
    {
        using var document = LoadFixture("screening-confidence-bounds-negative.json");
        var @case = document.RootElement.GetProperty("case");
        var criteria = BuildCriteria(@case.GetProperty("criteria"));
        var candidateSet = BuildCandidateSet(@case.GetProperty("candidateSet"), true);
        var service = new ScreeningService(candidateSet, [criteria]);
        var criteriaDigest = criteria.ComputeDigest().ToString();

        foreach (var decisionFixture in @case.GetProperty("decisions").EnumerateArray())
        {
            var expectedCategory = decisionFixture.GetProperty("expectedCategory").GetString()!;
            var decision = BuildDecision(
                decisionFixture,
                criteria.CriteriaId,
                criteriaDigest,
                candidateSet.CandidateSetId,
                allowAppend: true);

            var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
            Assert.AreEqual(expectedCategory, error.Category);
        }
    }

    [TestMethod]
    public void Screening_ai_suggestion_cannot_be_final_decision()
    {
        using var document = LoadFixture("screening-ai-suggestion-not-final.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCase(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var suggestion = BuildSuggestion(@case.GetProperty("suggestion"), candidateSet.CandidateSetId);
        service.AddSuggestion(suggestion);

        var badDecision = BuildDecision(@case.GetProperty("finalDecision"), criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId, forceAutomationActor: true);
        var expected = @case.GetProperty("expected");

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(badDecision));
        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_conflict_is_created_when_final_humans_disagree()
    {
        using var document = LoadFixture("screening-conflict-created-from-disagreement.json");
        var @case = document.RootElement.GetProperty("case");
        var (_, service, criteria, candidateSet) = BuildServiceCase(@case);
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var expected = @case.GetProperty("expected");

        var first = BuildDecision(@case.GetProperty("decisions")[0], criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        var second = BuildDecision(@case.GetProperty("decisions")[1], criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        service.AddDecision(first);
        service.AddDecision(second);

        Assert.AreEqual(1, service.Conflicts.Count);
        Assert.AreEqual(
            expected.GetProperty("candidateId").GetString(),
            service.Conflicts[0].CandidateId);
        Assert.AreEqual(
            expected.GetProperty("stage").GetString(),
            service.Conflicts[0].Stage);
        Assert.AreEqual(
            expected.GetProperty("resolved").GetBoolean(),
            service.Conflicts[0].Resolved);
    }

    [TestMethod]
    public void Screening_conflict_is_resolved_by_human_adjudication()
    {
        using var document = LoadFixture("screening-conflict-resolved-by-human.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCaseWithConflictingDecisionSet(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var expected = @case.GetProperty("expected");

        var sourceDecision = BuildDecision(@case.GetProperty("sourceDecisions")[0], criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        var sourceDecision2 = BuildDecision(@case.GetProperty("sourceDecisions")[1], criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        service.AddDecision(sourceDecision);
        service.AddDecision(sourceDecision2);
        Assert.AreEqual(1, service.Conflicts.Count);
        Assert.IsFalse(service.Conflicts[0].Resolved);

        var adjudication = BuildDecision(
            @case.GetProperty("adjudication"),
            criteria.CriteriaId,
            criteriaDigest,
            candidateSet.CandidateSetId,
            decisionKind: ScreeningDecisionKind.Adjudication);
        adjudication = BuildAdjudicationWithSourceLinks(
            adjudication,
            new[] { sourceDecision.DecisionId, sourceDecision2.DecisionId },
            service.Conflicts[0].ConflictId);

        service.AddDecision(adjudication);
        Assert.AreEqual(1, service.Decisions.Count(d => d.DecisionKind == ScreeningDecisionKind.Adjudication));
        Assert.AreEqual(1, service.Conflicts.Count);
        Assert.IsTrue(service.Conflicts[0].Resolved);
        Assert.AreEqual(expected.GetProperty("resolved").GetBoolean(), service.Conflicts[0].Resolved);
        Assert.AreEqual(expected.GetProperty("resolvedByDecisionId").GetString(), service.Conflicts[0].ResolvedByDecisionId);
    }

    [TestMethod]
    public void Screening_post_adjudication_disagreement_creates_next_conflict_generation()
    {
        using var document = LoadFixture("screening-conflict-resolved-by-human.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCaseWithConflictingDecisionSet(document.RootElement.GetProperty("case"));
        var digest = criteria.ComputeDigest().ToString();
        var first = BuildDecision(@case.GetProperty("sourceDecisions")[0], criteria.CriteriaId, digest, candidateSet.CandidateSetId);
        var second = BuildDecision(@case.GetProperty("sourceDecisions")[1], criteria.CriteriaId, digest, candidateSet.CandidateSetId);
        service.AddDecision(first);
        service.AddDecision(second);
        var firstConflict = service.Conflicts.Single();
        var adjudication = BuildAdjudicationWithSourceLinks(
            BuildDecision(@case.GetProperty("adjudication"), criteria.CriteriaId, digest, candidateSet.CandidateSetId, decisionKind: ScreeningDecisionKind.Adjudication),
            new[] { first.DecisionId, second.DecisionId },
            firstConflict.ConflictId);
        service.AddDecision(adjudication);

        service.AddDecision(new ScreeningDecision(
            "decision-post-adjudication-disagreement",
            candidateSet.CandidateSetId,
            first.CandidateId,
            null,
            null,
            first.Stage,
            ScreeningVerdicts.Include,
            ScreeningActor.Human("human-post-adjudication"),
            DateTimeOffset.Parse("2026-06-27T02:50:30Z"),
            "New evidence changes the human decision.",
            null,
            criteria.CriteriaId,
            digest,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>()));

        Assert.AreEqual(2, service.Conflicts.Count);
        Assert.AreEqual(2, service.Conflicts.Single(conflict => !conflict.Resolved).Generation);
    }

    [TestMethod]
    public void Screening_unresolved_conflict_blocks_downstream_stage_handoff()
    {
        using var document = LoadFixture("screening-unresolved-conflict-blocks-handoff.json");
        var @case = document.RootElement.GetProperty("case");
        var candidateSet = BuildCandidateSet(@case.GetProperty("candidateSet"), true);
        var titleCriteria = BuildCriteria(@case.GetProperty("titleCriteria"));
        var fullTextCriteria = BuildCriteria(@case.GetProperty("fullTextCriteria"));
        var service = new ScreeningService(candidateSet, [titleCriteria, fullTextCriteria]);
        var titleCriteriaDigest = titleCriteria.ComputeDigest().ToString();
        var fullTextCriteriaDigest = fullTextCriteria.ComputeDigest().ToString();
        var expected = @case.GetProperty("expected");

        var sourceDecision = BuildDecision(
            @case.GetProperty("sourceDecisions")[0],
            titleCriteria.CriteriaId,
            titleCriteriaDigest,
            candidateSet.CandidateSetId);
        var sourceDecision2 = BuildDecision(
            @case.GetProperty("sourceDecisions")[1],
            titleCriteria.CriteriaId,
            titleCriteriaDigest,
            candidateSet.CandidateSetId);
        service.AddDecision(sourceDecision);
        service.AddDecision(sourceDecision2);

        var blocked = BuildDecision(
            @case.GetProperty("fullTextDecision"),
            fullTextCriteria.CriteriaId,
            fullTextCriteriaDigest,
            candidateSet.CandidateSetId,
            stage: ScreeningStages.FullText);
        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(blocked));
        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_adjudication_preserves_source_decisions()
    {
        using var document = LoadFixture("screening-adjudication-source-decision-links.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCaseWithConflictingDecisionSet(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var sourceDecisionsProperty = @case.GetProperty("sourceDecisionIds");

        var source1 = BuildDecision(@case.GetProperty("sourceDecisions")[0], criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        var source2 = BuildDecision(@case.GetProperty("sourceDecisions")[1], criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        service.AddDecision(source1);
        service.AddDecision(source2);

        var adjudication = BuildDecision(@case.GetProperty("adjudication"), criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId, decisionKind: ScreeningDecisionKind.Adjudication);
        var expectedSourceIds = sourceDecisionsProperty.EnumerateArray().Select(item => item.GetString()!).ToArray();
        adjudication = BuildAdjudicationWithSourceLinks(
            adjudication,
            expectedSourceIds,
            service.Conflicts.Single().ConflictId);

        service.AddDecision(adjudication);
        var last = service.Decisions.Last(d => d.DecisionKind == ScreeningDecisionKind.Adjudication);
        CollectionAssert.AreEqual(expectedSourceIds, last.SourceDecisionIds.ToArray());
    }

    [TestMethod]
    public void Screening_web_projection_records_cannot_be_core_decision_identity()
    {
        using var document = LoadFixture("screening-app-assignment-projection-not-authority.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCase(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var decision = BuildDecision(@case.GetProperty("decision"), criteria.CriteriaId, criteriaDigest, candidateSet.CandidateSetId);
        var expected = @case.GetProperty("expected");

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    [TestMethod]
    public void Screening_cli_outputs_or_local_paths_do_not_become_core_identity()
    {
        using var document = LoadFixture("screening-cli-file-output-not-core-authority.json");
        var (@case, service, criteria, candidateSet) = BuildServiceCase(document.RootElement.GetProperty("case"));
        var criteriaDigest = criteria.ComputeDigest().ToString();
        var expected = @case.GetProperty("expected");

        var badDecision = BuildDecision(
            @case.GetProperty("decision"),
            criteria.CriteriaId,
            criteriaDigest,
            candidateSet.CandidateSetId,
            evidenceRefs: ReadStringArray(@case.GetProperty("decision").GetProperty("evidenceRefs")));

        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(badDecision));
        Assert.AreEqual(expected.GetProperty("category").GetString(), error.Category);
    }

    private static (JsonElement caseElement, ScreeningService service, ScreeningCriteria criteria, ScreeningCandidateSet candidateSet) BuildServiceCase(JsonElement caseElement)
    {
        var criteria = BuildCriteria(caseElement.GetProperty("criteria"));
        var candidateSet = BuildCandidateSet(caseElement.GetProperty("candidateSet"), true);
        var service = new ScreeningService(candidateSet, [criteria]);
        return (caseElement, service, criteria, candidateSet);
    }

    private static (JsonElement caseElement, ScreeningService service, ScreeningCriteria criteria, ScreeningCandidateSet candidateSet) BuildServiceCaseWithConflictingDecisionSet(JsonElement caseElement)
    {
        return BuildServiceCase(caseElement);
    }

    private static ScreeningCandidateSet BuildCandidateSet(JsonElement element, bool lockedDefault)
    {
        var locked = element.TryGetProperty("locked", out var lockedProperty)
            ? lockedProperty.GetBoolean()
            : lockedDefault;
        var sourceKind = element.TryGetProperty("sourceKind", out var sourceKindProperty)
            ? sourceKindProperty.GetString()!
            : ScreeningSourceKinds.DeduplicationResult;

        if (string.Equals(sourceKind, ScreeningSourceKinds.LockedReviewableCandidateSet, StringComparison.Ordinal))
        {
            var candidateIds = ReadStringArray(element.GetProperty("candidateIds"));
            var unresolvedCandidateIds = element.TryGetProperty("unresolvedCandidateIds", out var unresolvedCandidateIdsProperty)
                ? ReadStringArray(unresolvedCandidateIdsProperty)
                : Array.Empty<string>();

            return ScreeningCandidateSet.CreateLockedReviewableCandidateSet(
                element.GetProperty("candidateSetId").GetString() ?? throw new InvalidOperationException("Candidate set id is required."),
                candidateIds.Select(id => BuildCandidate(id)).ToArray(),
                unresolvedCandidateIds.Select(id => BuildCandidate(id, hasStableIdentifier: false)).ToArray(),
                element.TryGetProperty("sourceRefs", out var sourceRefs) ? ReadStringArray(sourceRefs) : Array.Empty<string>(),
                locked);
        }

        var dedup = BuildDedupResult(element.GetProperty("dedupResult"));
        return ScreeningService.CreateCandidateSetFromDedupResult(
            element.GetProperty("candidateSetId").GetString() ?? throw new InvalidOperationException("Candidate set id is required."),
            dedup,
            locked,
            sourceKind);
    }

    private static DeduplicationResult BuildDedupResult(JsonElement element)
    {
        var candidateIds = ReadStringArray(element.GetProperty("candidateIds"));
        var unresolvedCandidateIds = element.TryGetProperty("unresolvedCandidateIds", out var unresolvedCandidateIdsProperty)
            ? ReadStringArray(unresolvedCandidateIdsProperty)
            : Array.Empty<string>();
        var resultId = element.GetProperty("resultId").GetString()!;
        DedupCandidateRecord[] candidates = candidateIds.Select(id => BuildCandidate(id)).ToArray();
        DedupCandidateRecord[] unresolved = unresolvedCandidateIds.Select(id => BuildCandidate(id, hasStableIdentifier: false)).ToArray();

        return new DeduplicationResult(
            resultId,
            element.GetProperty("schemaId").GetString() ?? ScreeningServiceTestsSchemaAlias,
            element.GetProperty("schemaVersion").GetString() ?? "1.0.0",
            null,
            null,
            0.95d,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal)),
            Array.Empty<string>(),
            Array.Empty<string>(),
            candidates,
            Array.Empty<DedupCluster>(),
            Array.Empty<DedupEvidence>(),
            unresolved,
            Array.Empty<DedupReviewCandidate>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            new[] { "no-broad-php-screening-compatibility", "no-screening" });
    }

    private static DedupCandidateRecord BuildCandidate(string candidateId, bool hasStableIdentifier = true)
    {
        return new DedupCandidateRecord(
            candidateId,
            $"Candidate {candidateId}",
            hasStableIdentifier,
            hasStableIdentifier ? $"work-{candidateId}" : null,
            hasStableIdentifier ? new[] { $"work-{candidateId}" } : Array.Empty<string>(),
            Array.Empty<string>(),
            new DedupSightingRef(
                "search",
                "trace-screening",
                $"sighting-{candidateId}",
                ProviderAlias: "openalex"));
    }

    private static ScreeningCriteria BuildCriteria(JsonElement element)
    {
        var include = CanonicalJsonValue.FromJsonElement(element.GetProperty("include"));
        var exclude = CanonicalJsonValue.FromJsonElement(element.GetProperty("exclude"));
        var criteriaId = element.GetProperty("criteriaId").GetString()!;
        var criteriaVersion = element.GetProperty("criteriaVersion").GetString()!;
        var stage = element.GetProperty("stage").GetString()!;
        var requiresProtocolBinding = element.GetProperty("requiresProtocolBinding").GetBoolean();
        var workflowBinding = element.TryGetProperty("workflowBinding", out var workflow) ? workflow.GetString() : null;
        var approvedProtocolBinding = element.TryGetProperty("approvedProtocolBinding", out var approvedBinding)
            ? approvedBinding.GetString()
            : null;
        var approvedProtocolDigest = element.TryGetProperty("approvedProtocolDigest", out var approvedDigest)
            ? approvedDigest.GetString()
            : null;
        var digestScopeHint = element.TryGetProperty("digestScopeHint", out var digestScopeHintElement)
            ? digestScopeHintElement.GetString()
            : null;
        var approvedProtocolDigestScope = element.TryGetProperty("approvedProtocolDigestScope", out var approvedProtocolDigestScopeElement)
            ? approvedProtocolDigestScopeElement.GetString()
            : DigestScope.ProtocolContent.ToString();
        var approvedProtocolStatus = element.TryGetProperty("approvedProtocolStatus", out var approvedProtocolStatusElement)
            ? approvedProtocolStatusElement.GetString()
            : ScreeningProtocolBindingStatus.Approved;
        var currentProtocolContentDigest = element.TryGetProperty("currentProtocolContentDigest", out var currentProtocolContentDigestElement)
            ? currentProtocolContentDigestElement.GetString()
            : approvedProtocolDigest ?? ApprovedProtocolDigest;
        var reviewGuidance = element.TryGetProperty("reviewGuidance", out var reviewGuidanceElement)
            ? reviewGuidanceElement.GetString()
            : null;

        return new ScreeningCriteria(
            criteriaId,
            criteriaVersion,
            stage,
            include,
            exclude,
            requiresProtocolBinding,
            approvedProtocolBinding,
            approvedProtocolDigest,
            workflowBinding,
            reviewGuidance,
            element.TryGetProperty("fullTextRequirements", out var fullTextRequirements)
                ? CanonicalJsonValue.FromJsonElement(fullTextRequirements)
                : null,
            digestScopeHint,
            approvedProtocolDigestScope,
            approvedProtocolStatus,
            currentProtocolContentDigest);
    }

    private static ScreeningDecision BuildAdjudicationWithSourceLinks(
        ScreeningDecision adjudication,
        IReadOnlyList<string> sourceDecisionIds,
        string resolvedConflictId)
    {
        return new ScreeningDecision(
            adjudication.DecisionId,
            adjudication.CandidateSetId,
            adjudication.CandidateId,
            adjudication.WorkId,
            adjudication.DedupClusterId,
            adjudication.Stage,
            adjudication.Verdict,
            adjudication.Actor,
            adjudication.DecidedAt,
            adjudication.Rationale,
            adjudication.Confidence,
            adjudication.CriteriaRef,
            adjudication.CriteriaDigest,
            adjudication.EvidenceRefs,
            sourceDecisionIds,
            adjudication.SourceSuggestionIds,
            adjudication.ConflictId,
            resolvedConflictId,
            ScreeningDecisionKind.Adjudication,
            adjudication.NonClaims);
    }

    private static ScreeningDecision BuildDecision(
        JsonElement element,
        string criteriaId,
        string criteriaDigest,
        string candidateSetId,
        ScreeningDecisionKind decisionKind = ScreeningDecisionKind.Human,
        bool omitActor = false,
        bool omitRationale = false,
        string? stage = null,
        bool forceAutomationActor = false,
        bool allowAppend = true,
        IReadOnlyList<string>? evidenceRefs = null)
    {
        var actorProperty = element.GetProperty("actor");
        var actor = omitActor
            ? null
            : forceAutomationActor
                ? ScreeningActor.Automation(actorProperty.GetProperty("actorId").GetString()!)
                : ScreeningActor.Human(actorProperty.GetProperty("actorId").GetString()!);

        return new ScreeningDecision(
            element.GetProperty("decisionId").GetString()!,
            candidateSetId,
            element.GetProperty("candidateId").GetString()!,
            element.TryGetProperty("workId", out var workId) ? workId.GetString() : null,
            element.TryGetProperty("dedupClusterId", out var dedupClusterId) ? dedupClusterId.GetString() : null,
            stage ?? element.GetProperty("stage").GetString()!,
            element.GetProperty("verdict").GetString()!,
            actor,
            DateTimeOffset.Parse(element.GetProperty("decidedAt").GetString()!),
            omitRationale ? null : element.GetProperty("rationale").GetString()!,
            element.TryGetProperty("confidence", out var confidence) ? confidence.GetDouble() : null,
            criteriaId,
            criteriaDigest,
            evidenceRefs ?? (element.TryGetProperty("evidenceRefs", out var refs) ? ReadStringArray(refs) : Array.Empty<string>()),
            element.TryGetProperty("sourceDecisionIds", out var sourceDecisionIds)
                ? ReadStringArray(sourceDecisionIds)
                : Array.Empty<string>(),
            element.TryGetProperty("sourceSuggestionIds", out var sourceSuggestionIds)
                ? ReadStringArray(sourceSuggestionIds)
                : Array.Empty<string>(),
            element.TryGetProperty("conflictId", out var conflictId) ? conflictId.GetString() : null,
            element.TryGetProperty("resolvedConflictId", out var resolvedConflictId) ? resolvedConflictId.GetString() : null,
            decisionKind,
            element.TryGetProperty("nonClaims", out var nonClaims)
                ? ReadStringArray(nonClaims)
                : Array.Empty<string>());
    }

    private static ScreeningSuggestion BuildSuggestion(JsonElement element, string candidateSetId)
    {
        return new ScreeningSuggestion(
            element.GetProperty("suggestionId").GetString()!,
            candidateSetId,
            element.GetProperty("candidateId").GetString()!,
            element.GetProperty("stage").GetString()!,
            element.GetProperty("verdict").GetString()!,
            element.TryGetProperty("confidence", out var confidence) ? confidence.GetDouble() : null,
            element.TryGetProperty("rationale", out var rationale) ? rationale.GetString() : null,
            element.TryGetProperty("promptDigest", out var promptDigest) ? promptDigest.GetString() : null,
            element.TryGetProperty("responseDigest", out var responseDigest) ? responseDigest.GetString() : null,
            element.TryGetProperty("evidenceRefs", out var refs) ? ReadStringArray(refs) : Array.Empty<string>(),
            element.TryGetProperty("sourceDecisionId", out var sourceDecisionId) ? sourceDecisionId.GetString() : null,
            element.TryGetProperty("nonClaims", out var nonClaims)
                ? ReadStringArray(nonClaims)
                : Array.Empty<string>());
    }

    private static SearchTrace BuildSearchTrace(JsonElement element)
    {
        var cacheYearFrom = element.TryGetProperty("yearFrom", out var cacheYearFromElement) ? (int?)cacheYearFromElement.GetInt32() : null;
        var cacheYearTo = element.TryGetProperty("yearTo", out var cacheYearToElement) ? (int?)cacheYearToElement.GetInt32() : null;
        var cacheLanguage = element.TryGetProperty("language", out var cacheLanguageElement) ? cacheLanguageElement.GetString() : null;

        var cacheIdentity = SearchCacheIdentity.Compute(
            new SearchQueryInput(
                element.GetProperty("query").GetString()!,
                cacheYearFrom,
                cacheYearTo,
                cacheLanguage,
                element.GetProperty("maxResults").GetInt32(),
                element.GetProperty("offset").GetInt32(),
                element.GetProperty("includeRawData").GetBoolean(),
                Array.Empty<string>()),
            element.GetProperty("validationYear").GetInt32(),
            Array.Empty<string>());

        return new SearchTrace(
            element.GetProperty("traceId").GetString()!,
            SearchTrace.TraceSchemaId,
            SearchTrace.TraceSchemaVersion,
            new SearchTraceRequest(
                element.GetProperty("query").GetString()!,
                SearchYearRange.Validate(
                    element.TryGetProperty("yearFrom", out var requestYearFrom) ? requestYearFrom.GetInt32() : null,
                    element.TryGetProperty("yearTo", out var requestYearTo) ? requestYearTo.GetInt32() : null,
                    element.GetProperty("validationYear").GetInt32()),
                element.TryGetProperty("language", out var requestLanguage) ? requestLanguage.GetString() : null,
                element.GetProperty("maxResults").GetInt32(),
                element.GetProperty("offset").GetInt32(),
                element.GetProperty("includeRawData").GetBoolean(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                null),
            cacheIdentity,
            Array.Empty<SearchProviderAttempt>(),
            Array.Empty<SearchProviderStat>(),
            Array.Empty<SearchSighting>(),
            new SearchSummary(0, 0, 0, 0, false),
            SearchTrace.DefaultNonClaims);
    }

    private static void ValidateHumanDecision(JsonElement caseElement)
    {
        var (_, service, criteria, candidateSet) = BuildServiceCase(caseElement);
        var decisionCase = caseElement.GetProperty("decision");
        var expected = caseElement.GetProperty("expected");
        var decision = BuildDecision(
            decisionCase,
            criteria.CriteriaId,
            criteria.ComputeDigest().ToString(),
            candidateSet.CandidateSetId,
            stage: expected.TryGetProperty("stage", out var stage) ? stage.GetString() : null);
        service.AddDecision(decision);

        Assert.AreEqual(1, service.Decisions.Count);
        Assert.AreEqual(decision.Verdict, service.Decisions[0].Verdict);
        Assert.AreEqual(expected.GetProperty("verdict").GetString(), service.Decisions[0].Verdict);
        Assert.AreEqual(0, service.Conflicts.Count);
    }

    private static string ScreeningServiceTestsSchemaAlias => "nexus.deduplication.result";

    private static string[] ReadStringArray(JsonElement root)
    {
        return root.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureDirectory, fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
