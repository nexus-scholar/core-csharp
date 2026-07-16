using System.Text.Json;
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

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ReportingFixtureTests
{
    private const string FixtureSchema = "nexus.fe06.reporting.fixture-catalog.v1";
    private const string RequiredSourceKind = "local-fe06-reporting-contract";
    private const string RequiredSourceCommit = "7818640";
    private const string GeneratorVersion = "fe06-reporting-local-v1";
    private const string GeneratorCommand = "C:\\Users\\mouadh\\.dotnet\\dotnet.exe test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --filter FullyQualifiedName~ReportingFixtureTests";
    private const string SchemaIdExpected = "nexus-jcs-nfc-v1";
    private static readonly string[] RequiredFixtureIds =
    [
        "reporting-finalized-v1",
        "reporting-missing-full-text-gap-v1"
    ];
    private static readonly string[] RequiredNoClaims =
    [
        "no-php-compatibility-claim",
        "no-prisma-certification-claim"
    ];
    private static readonly DateTimeOffset Clock = new(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Fe06_1_reporting_fixture_catalog_is_valid_and_complete()
    {
        using var catalog = LoadFixture("catalog.json");
        var root = catalog.RootElement;
        Assert.AreEqual(FixtureSchema, root.GetProperty("schema").GetString());
        Assert.AreEqual(SchemaIdExpected, root.GetProperty("canonicalizationProfile").GetString());
        Assert.IsTrue(ContainsAll(root.GetProperty("nonClaims"), RequiredNoClaims));
        Assert.IsTrue(ContainsAll(root.GetProperty("comparisonRules"), new[] { "no-php-compatibility-claim", "no-prisma-certification-claim" }));

        var fixtureIds = root.GetProperty("fixtures").EnumerateArray()
            .Select(item => item.GetString())
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(RequiredFixtureIds, fixtureIds);
        Assert.AreEqual(GeneratorVersion, root.GetProperty("generatorVersion").GetString());
        Assert.AreEqual(GeneratorCommand, root.GetProperty("generatorCommand").GetString());

        foreach (var fixtureId in RequiredFixtureIds)
        {
            using var fixture = LoadFixture($"{fixtureId}.json");
            var fixtureRoot = fixture.RootElement;

            Assert.AreEqual(fixtureId, fixtureRoot.GetProperty("fixtureId").GetString());
            Assert.AreEqual(RequiredSourceKind, fixtureRoot.GetProperty("sourceKind").GetString(), fixtureId);
            Assert.AreEqual(RequiredSourceCommit, fixtureRoot.GetProperty("sourceCommit").GetString(), fixtureId);
            Assert.AreEqual(GeneratorVersion, fixtureRoot.GetProperty("generatorVersion").GetString(), fixtureId);
            Assert.AreEqual(GeneratorCommand, fixtureRoot.GetProperty("generatorCommand").GetString(), fixtureId);
            Assert.IsTrue(ContainsAll(fixtureRoot.GetProperty("comparisonRules"), new[] { "no-php-compatibility-claim", "no-prisma-certification-claim" }));
            Assert.IsTrue(ContainsAll(fixtureRoot.GetProperty("nonClaims"), RequiredNoClaims));
            Assert.IsNotNull(fixtureRoot.GetProperty("sourceRefs").EnumerateArray().SingleOrDefault(item => string.Equals(item.GetString(), "docs/adr/0033-reporting-audit-bundle-and-rapid-review-profile.md", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Fe06_1_reporting_fixtures_replay_projection_and_validate_reporting_outputs()
    {
        foreach (var fixtureId in RequiredFixtureIds)
        {
            using var fixture = LoadFixture($"{fixtureId}.json");
            var root = fixture.RootElement;
            var input = root.GetProperty("input");
            var expected = root.GetProperty("expected");
            var includeFullText = input.GetProperty("includeFullText").GetBoolean();
            var disclosures = ReadTextArray(input.GetProperty("disclosures")).ToArray();
            var fixtureNonClaims = ReadTextArray(root.GetProperty("nonClaims")).ToArray();

            var authorities = BuildAuthorities(includeFullText);
            var projection = ReviewFlowProjector.Project(authorities, disclosures, fixtureNonClaims);

            AssertCounts(expected.GetProperty("counts"), projection.Counts, fixtureId);
            AssertReasonTotals(expected.GetProperty("titleAbstractReasons"), projection.TitleAbstractReasons, fixtureId);
            AssertReasonTotals(expected.GetProperty("fullTextReasons"), projection.FullTextReasons, fixtureId);
            AssertAudit(expected.GetProperty("audit"), projection.Audit, fixtureId);
            AssertGapCandidates(expected.GetProperty("gaps"), projection.Gaps, fixtureId);

            var finalizable = expected.GetProperty("finalizable").GetBoolean();
            if (finalizable)
            {
                var report = ReviewFlowProjector.Finalize(projection);
                var expectedSliceSchemaId = expected.GetProperty("schemas").GetProperty("slice").GetProperty("schemaId").GetString();
                var expectedSliceSchemaVersion = expected.GetProperty("schemas").GetProperty("slice").GetProperty("schemaVersion").GetString();
                var expectedReportSchemaId = expected.GetProperty("schemas").GetProperty("report").GetProperty("schemaId").GetString();
                var expectedReportSchemaVersion = expected.GetProperty("schemas").GetProperty("report").GetProperty("schemaVersion").GetString();

                Assert.AreEqual(expectedSliceSchemaId, report.SliceEnvelope.SchemaId, fixtureId);
                Assert.AreEqual(expectedSliceSchemaVersion, report.SliceEnvelope.SchemaVersion, fixtureId);
                Assert.AreEqual(expectedReportSchemaId, report.ReportEnvelope.SchemaId, fixtureId);
                Assert.AreEqual(expectedReportSchemaVersion, report.ReportEnvelope.SchemaVersion, fixtureId);
                Assert.AreEqual(ReportingSchemas.SliceBindingId, report.SliceEnvelope.SchemaId);
                Assert.AreEqual(ReportingSchemas.ReportId, report.ReportEnvelope.SchemaId);
                Assert.AreEqual(ReportingSchemas.Version, report.SliceEnvelope.SchemaVersion);
                Assert.AreEqual(ReportingSchemas.Version, report.ReportEnvelope.SchemaVersion);
                var sliceBytes = ReportingCanonicalCodec.SerializeSlice(report);
                var reportBytes = ReportingCanonicalCodec.SerializeReport(report);
                CollectionAssert.AreEqual(sliceBytes, ReportingCanonicalCodec.SerializeSlice(ReviewFlowProjector.Finalize(projection)), fixtureId);
                CollectionAssert.AreEqual(reportBytes, ReportingCanonicalCodec.SerializeReport(ReviewFlowProjector.Finalize(projection)), fixtureId);
                CollectionAssert.AreEqual(ReviewFlowMarkdownRenderer.Render(report),
                    ReviewFlowMarkdownRenderer.Render(ReportingCanonicalCodec.Rehydrate(sliceBytes, reportBytes, projection)), fixtureId);
            }
            else
            {
                var expectedCategory = expected.GetProperty("finalizationErrorCategory").GetString();
                var error = Assert.ThrowsExactly<ReportingRuleException>(() => ReviewFlowProjector.Finalize(projection), fixtureId);
                Assert.AreEqual(expectedCategory, error.Category, fixtureId);
            }
        }
    }

    private static void AssertCounts(JsonElement expectedCounts, ReviewFlowCounts actual, string fixtureId)
    {
        Assert.AreEqual(expectedCounts.GetProperty("identified").GetInt32(), actual.Identified, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("duplicatesConsolidated").GetInt32(), actual.DuplicatesConsolidated, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("postDedup").GetInt32(), actual.PostDedup, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("titleAbstractIncluded").GetInt32(), actual.TitleAbstractIncluded, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("titleAbstractExcluded").GetInt32(), actual.TitleAbstractExcluded, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("fullTextIncluded").GetInt32(), actual.FullTextIncluded, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("fullTextExcluded").GetInt32(), actual.FullTextExcluded, fixtureId);
        Assert.AreEqual(expectedCounts.GetProperty("included").GetInt32(), actual.Included, fixtureId);
    }

    private static void AssertReasonTotals(JsonElement expectedReasons, IReadOnlyList<ReviewReasonCount> actual, string fixtureId)
    {
        var expected = expectedReasons.EnumerateArray()
            .Select(item => new ReviewReasonCount(item.GetProperty("code").GetString()!, item.GetProperty("count").GetInt32()))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
        var actualOrdered = actual.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray();

        Assert.AreEqual(expected.Length, actualOrdered.Length, $"reason bucket count mismatch for {fixtureId}");
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i].Code, actualOrdered[i].Code, fixtureId);
            Assert.AreEqual(expected[i].Count, actualOrdered[i].Count, fixtureId);
        }
    }

    private static void AssertAudit(JsonElement expectedAudit, ReviewAuditCounts actual, string fixtureId)
    {
        Assert.AreEqual(expectedAudit.GetProperty("conflicts").GetInt32(), actual.Conflicts, fixtureId);
        Assert.AreEqual(expectedAudit.GetProperty("adjudications").GetInt32(), actual.Adjudications, fixtureId);
        Assert.AreEqual(expectedAudit.GetProperty("corrections").GetInt32(), actual.Corrections, fixtureId);
        Assert.AreEqual(expectedAudit.GetProperty("invalidations").GetInt32(), actual.Invalidations, fixtureId);
    }

    private static void AssertGapCandidates(JsonElement expectedGaps, IReadOnlyList<ReviewFlowGap> actual, string fixtureId)
    {
        var expectedCandidates = expectedGaps.EnumerateArray()
            .Select(item => item.GetProperty("candidateId").GetString()!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var expectedCategory = expectedGaps.EnumerateArray()
            .Select(item => item.GetProperty("category").GetString()!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var actualCandidates = actual.Select(item => item.CandidateId).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var actualCategory = actual.Select(item => item.Category).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedCandidates, actualCandidates, fixtureId);
        CollectionAssert.AreEqual(expectedCategory, actualCategory, fixtureId);

        if (expectedGaps.GetArrayLength() == 0)
        {
            Assert.AreEqual(0, actual.Count, fixtureId);
        }
        else
        {
            Assert.AreEqual(expectedCategory.Length, actual.Count, fixtureId);
        }
    }

    private static bool ContainsAll(JsonElement items, string[] expected)
    {
        var actual = items.EnumerateArray().Select(item => item.GetString()).ToHashSet(StringComparer.Ordinal);
        return expected.All(item => actual.Contains(item));
    }

    private static IEnumerable<string> ReadTextArray(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
            yield return item.GetString()!;
    }

    private static JsonDocument LoadFixture(string fileName)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(ReportingFixtureDirectory(), fileName)));
    }

    private static string ReportingFixtureDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "fixtures", "reporting");
    }

    private static ReviewSliceAuthorities BuildAuthorities(bool includeFullText)
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
            [new ScreeningExclusionReason("wrong-population", ScreeningStages.TitleAbstract)], actor, Clock);
        var header = ScreeningConductHeader.Create("conduct-reporting", snapshotPolicy.Policy, actor, Clock);
        var include = ScreeningConductDecision.Create(header, 1, header.Digest, "request-a", "candidate-a",
            ScreeningConductDecisionKind.Review, ScreeningVerdicts.Include, actor, "Advance.", Clock);
        var exclude = ScreeningConductDecision.Create(header, 2, include.Digest, "request-c", "candidate-c",
            ScreeningConductDecisionKind.Review, ScreeningVerdicts.Exclude, actor, "Exclude.", Clock,
            exclusionReasonCode: "wrong-population");
        var journal = ScreeningConductJournal.Rehydrate(header, snapshotPolicy.Policy, [include, exclude]);
        var handoff = ScreeningConductHandoff.Create("handoff-reporting", journal, Clock);
        var cases = includeFullText
            ? new[] { BuildFullTextCase(protocol, dedup, journal, handoff, actor) }
            : [];
        var cut = new VerifiedReviewWorkspaceCut(
            "workspace-reporting", 4,
            [
                new ReviewGenerationBinding(ReviewGenerationRoles.Protocol, "generation-protocol", ContentDigest.Sha256Utf8("manifest-protocol")),
                new ReviewGenerationBinding(ReviewGenerationRoles.Workflow, "generation-workflow", ContentDigest.Sha256Utf8("manifest-workflow")),
                new ReviewGenerationBinding(ReviewGenerationRoles.Deduplication, "generation-deduplication", ContentDigest.Sha256Utf8("manifest-deduplication")),
                new ReviewGenerationBinding(ReviewGenerationRoles.CorpusSnapshot, "generation-snapshot", ContentDigest.Sha256Utf8("manifest-snapshot")),
                new ReviewGenerationBinding(ReviewGenerationRoles.ScreeningConduct, "generation-screening", ContentDigest.Sha256Utf8("manifest-screening")),
                .. cases.Select(item => new ReviewGenerationBinding(
                    ReviewGenerationRoles.FullText,
                    "generation-fulltext",
                    ContentDigest.Sha256Utf8("manifest-fulltext"),
                    item.Admission.CandidateId))
            ]);
        var workflow = new VerifiedReportingWorkflowAuthority(
            "workflow-reporting", ContentDigest.Sha256Utf8("workflow-reporting"), protocol.Version.Id, protocol.Version.ContentDigest);
        return new ReviewSliceAuthorities(protocol, workflow, dedup, snapshot, snapshotPolicy, journal, handoff,
            cases, [], [], [], cut);
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
            new FullTextActor(actor.ActorId, FullTextActorKinds.Human), Clock, FullTextAttemptStatuses.Success,
            [new FullTextSourceAttempt("attempt-reporting", "local", 1, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success, artifactKind: FullTextArtifactKinds.Text, mediaType: "text/plain", artifactEvidenceId: "artifact-reporting")],
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
            [new ScreeningExclusionReason("wrong-population-full", ScreeningStages.FullText)], actor, Clock, rawDigest);
        var header = FullTextScreeningConductHeader.Create("full-conduct-reporting", policy, actor, Clock);
        var decision = FullTextScreeningConductDecision.Create(
            header, 1, header.Digest, "full-request-a", admission.CandidateId, ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, actor, "Include.", Clock,
            evidence: [new ScreeningConductEvidenceRef(FullTextScreeningConductEvidenceKinds.FullTextArtifact, artifact.ArtifactId, rawDigest)]);
        var journal = FullTextScreeningConductJournal.Create(policy, header);
        journal.Append(decision);
        return new FullTextReviewCaseAuthorities(admission, chain, journal, journal.CreateHandoff("full-handoff-reporting", Clock));
    }

    private static VerifiedDeduplicationAuthorityResultDigest BuildDeduplication(string policyId)
    {
        var a = Candidate("candidate-a", true);
        var b = Candidate("candidate-b", true);
        var c = Candidate("candidate-c", false);
        var cluster = new DedupCluster(
            "cluster-a-b", [a, b],
            new DedupRepresentativeResult(a.CandidateId, a.Title, a.PrimaryWorkId, a.WorkIds, [a.Source.SourceSightingId], 1d, []),
            [new DedupEvidence("evidence-a-b", DedupEvidenceKind.SourceSighting, a.CandidateId, b.CandidateId, "source-sighting", true, 0.99d, policyId, DeduplicationService.PolicyVersion)]);
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
            ], false, "alice", "owner", Clock, null, null, null));

    private static DedupCandidateRecord Candidate(string id, bool stable) => new(
        id,
        $"Title {id}",
        stable,
        stable ? $"doi:{id}" : null,
        stable ? [$"work:{id}"] : [],
        [],
        new DedupSightingRef("search", $"trace-{id}", $"sighting-{id}", "provider", "tool"));

    private static VerifiedProtocolVersion BuildProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-reporting-v1", "protocol-reporting", "project-reporting", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("reporting", "report review flow"), new CanonicalJsonObject(), [], [], [],
            ContentDigest.Sha256Utf8("placeholder"), ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId, ["approval-1"], Clock);
        var version = new ProtocolVersion(seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template, seed.Intent, seed.Values,
            seed.RequiredDecisions, seed.Decisions, seed.Waivers, seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private static ScreeningCriteria Criteria(VerifiedProtocolVersion protocol, string stage) => new(
        $"criteria-{stage}", "1.0.0", stage, CanonicalJsonValue.From("include"), CanonicalJsonValue.From("exclude"), true,
        protocol.Version.Id, protocol.Version.ContentDigest.ToString(), approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved, currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Clock;
    }
}
