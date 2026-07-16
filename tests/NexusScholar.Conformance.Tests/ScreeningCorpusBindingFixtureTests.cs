using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Screening.CorpusSnapshots;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ScreeningCorpusBindingFixtureTests
{
    private static readonly IClock Clock = new FixedClock(new DateTimeOffset(2026, 7, 16, 2, 30, 0, TimeSpan.Zero));

    [TestMethod]
    public void Screening_corpus_binding_fixtures_reproduce_and_execute_stable_categories()
    {
        var generated = Generate();
        if (Environment.GetEnvironmentVariable("UPDATE_FE06_FIXTURES") == "1")
            WriteFixtures(generated);

        foreach (var item in generated)
            CollectionAssert.AreEqual(File.ReadAllBytes(FixturePath(item.Key)), item.Value, item.Key);

        var authority = BuildAuthority();
        var binding = ScreeningCorpusBindingAuthority.Create("binding-fe06-fixture", authority.SourceResult, authority.Snapshot);
        var reopened = ScreeningCorpusBindingCanonicalCodec.Rehydrate(
            generated["binding-valid.json"], binding.BindingDigest, authority.SourceResult, authority.Snapshot);
        Assert.AreEqual(binding.BindingDigest, reopened.BindingDigest);

        foreach (var scenarioFile in generated.Keys.Where(name => name.StartsWith("scenario-", StringComparison.Ordinal)))
        {
            using var document = JsonDocument.Parse(generated[scenarioFile]);
            var scenario = document.RootElement.GetProperty("scenario").GetString()!;
            var expected = document.RootElement.GetProperty("expected_category").GetString()!;
            Assert.AreEqual(expected, ExecuteScenario(scenario, authority, binding), scenarioFile);
        }
    }

    private static string ExecuteScenario(string scenario, Authority authority, VerifiedScreeningCorpusBinding binding)
    {
        try
        {
            if (scenario == "stale-source")
            {
                ScreeningCorpusBindingAuthority.Create(
                    binding.BindingId,
                    BuildSource(authority.Policy.PolicyId, "other-result"),
                    authority.Snapshot);
                return "accepted";
            }

            var groupUnits = binding.GroupUnits.ToArray();
            var sourceResultDigest = binding.SourceResultDigest;
            var snapshotRecordDigest = binding.SnapshotRecordDigest;
            var decisionSetDigest = binding.DecisionSetDigest;
            switch (scenario)
            {
                case "altered-representative":
                    groupUnits[0] = groupUnits[0] with { ScreeningCandidateId = groupUnits[0].MemberCandidateIds[1] };
                    break;
                case "duplicate-unit":
                    groupUnits = [groupUnits[0], groupUnits[0], .. groupUnits.Skip(1)];
                    break;
                case "missing-unit":
                    groupUnits = groupUnits.Skip(1).ToArray();
                    break;
                case "missing-group-member":
                    groupUnits[0] = groupUnits[0] with { MemberCandidateIds = [groupUnits[0].ScreeningCandidateId] };
                    break;
                case "wrong-decision-set-digest":
                    decisionSetDigest = ContentDigest.Sha256Utf8("wrong-decision-set");
                    break;
                case "wrong-snapshot-digest":
                    snapshotRecordDigest = ContentDigest.Sha256Utf8("wrong-snapshot");
                    break;
                case "noncanonical-order":
                    groupUnits[0] = groupUnits[0] with { MemberCandidateIds = groupUnits[0].MemberCandidateIds.Reverse().ToArray() };
                    break;
                default:
                    throw new AssertFailedException($"Unknown FE-06.0 fixture scenario '{scenario}'.");
            }

            ScreeningCorpusBindingAuthority.Rehydrate(
                new UnverifiedScreeningCorpusBinding(
                    ScreeningCorpusBindingConstants.SchemaId,
                    ScreeningCorpusBindingConstants.SchemaVersion,
                    binding.BindingId,
                    binding.SourceResultId,
                    sourceResultDigest,
                    binding.SnapshotId,
                    snapshotRecordDigest,
                    decisionSetDigest,
                    groupUnits,
                    binding.UnresolvedUnits,
                    binding.BindingDigest),
                authority.SourceResult,
                authority.Snapshot);
            return "accepted";
        }
        catch (ScreeningCorpusBindingException exception)
        {
            return exception.Category;
        }
    }

    private static Dictionary<string, byte[]> Generate()
    {
        var authority = BuildAuthority();
        var binding = ScreeningCorpusBindingAuthority.Create("binding-fe06-fixture", authority.SourceResult, authority.Snapshot);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["binding-valid.json"] = ScreeningCorpusBindingCanonicalCodec.Serialize(binding),
            ["scenario-stale-source.json"] = Scenario("stale-source", ScreeningCorpusBindingErrorCodes.StaleSourceBinding),
            ["scenario-altered-representative.json"] = Scenario("altered-representative", ScreeningCorpusBindingErrorCodes.NonCanonicalBinding),
            ["scenario-duplicate-unit.json"] = Scenario("duplicate-unit", ScreeningCorpusBindingErrorCodes.NonCanonicalBinding),
            ["scenario-missing-unit.json"] = Scenario("missing-unit", ScreeningCorpusBindingErrorCodes.NonCanonicalBinding),
            ["scenario-missing-group-member.json"] = Scenario("missing-group-member", ScreeningCorpusBindingErrorCodes.NonCanonicalBinding),
            ["scenario-wrong-decision-set-digest.json"] = Scenario("wrong-decision-set-digest", ScreeningCorpusBindingErrorCodes.StaleSourceBinding),
            ["scenario-wrong-snapshot-digest.json"] = Scenario("wrong-snapshot-digest", ScreeningCorpusBindingErrorCodes.StaleSourceBinding),
            ["scenario-noncanonical-order.json"] = Scenario("noncanonical-order", ScreeningCorpusBindingErrorCodes.NonCanonicalBinding)
        };
        var manifest = new CanonicalJsonObject()
            .Add("schema", "nexus.fe06.screening-corpus-binding-fixtures.v1")
            .Add("canonicalization_profile", CanonicalJsonSerializer.ProfileId)
            .Add("generator", "UPDATE_FE06_FIXTURES=1 dotnet test --filter FullyQualifiedName~ScreeningCorpusBindingFixtureTests")
            .Add("source", "ADR 0033 local C# contract; no PHP or blueprint compatibility claim")
            .Add("files", CanonicalJsonValue.Array(files.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("name", item.Key)
                    .Add("sha256", ContentDigest.Sha256(item.Value).ToString()))
                .ToArray()));
        files["manifest.json"] = CanonicalJsonSerializer.SerializeToUtf8Bytes(manifest);
        return files;
    }

    private static byte[] Scenario(string scenario, string expectedCategory) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes(new CanonicalJsonObject()
            .Add("fixture_id", $"screening-corpus-binding-{scenario}")
            .Add("scope", "local-csharp-contract")
            .Add("scenario", scenario)
            .Add("expected_category", expectedCategory)
            .Add("non_claims", CanonicalJsonValue.Array([
                CanonicalJsonValue.From("no-php-compatibility-claim"),
                CanonicalJsonValue.From("no-blueprint-compatibility-claim")])));

    private static Authority BuildAuthority()
    {
        var policy = BuildPolicy();
        var source = BuildSource(policy.PolicyId, "result-fe06-fixture");
        var snapshot = CorpusSnapshotService.CreateBaseline(
            "snapshot-fe06-fixture", source, policy, policy.IssuedByActorId, policy.IssuedByRole, Clock);
        return new Authority(policy, source, snapshot);
    }

    private static VerifiedDeduplicationAuthorityPolicy BuildPolicy() =>
        DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
            DeduplicationAuthorityPolicyConstants.SchemaId,
            DeduplicationAuthorityPolicyConstants.SchemaVersion,
            DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            DeduplicationService.PolicyId,
            DeduplicationService.PolicyVersion,
            [new DeduplicationAuthorityPolicyActorRole("alice", "owner")],
            DeduplicationAuthorityPolicyConstants.ClosedActions,
            [
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, ["duplicate"]),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, ["different"]),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, ["uncertain"])
            ],
            false,
            "alice",
            "owner",
            Clock.UtcNow));

    private static VerifiedDeduplicationAuthorityResultDigest BuildSource(string policyId, string resultId)
    {
        var stableA = Candidate("candidate-a", true);
        var stableB = Candidate("candidate-b", true);
        var unresolved = Candidate("candidate-c", false);
        var cluster = new DedupCluster(
            "cluster-a-b",
            [stableA, stableB],
            new DedupRepresentativeResult(
                stableA.CandidateId,
                stableA.Title,
                stableA.PrimaryWorkId,
                stableA.WorkIds,
                [stableA.Source.SourceSightingId],
                1d,
                []),
            [new DedupEvidence(
                "evidence-a-b",
                DedupEvidenceKind.SourceSighting,
                stableA.CandidateId,
                stableB.CandidateId,
                "source-sighting",
                true,
                0.99d,
                policyId,
                DeduplicationService.PolicyVersion)]);
        var result = new DeduplicationResult(
            resultId,
            DeduplicationAuthorityDigests.ResultSchemaId,
            DeduplicationAuthorityDigests.ResultSchemaVersion,
            policyId,
            DeduplicationService.PolicyVersion,
            0.95d,
            new Dictionary<string, int>(),
            [],
            [],
            [stableA, stableB, unresolved],
            [cluster],
            [],
            [unresolved],
            [],
            [],
            [],
            ["no-php-compatibility-claim", "no-blueprint-compatibility-claim"]);
        return DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
    }

    private static DedupCandidateRecord Candidate(string id, bool stable) => new(
        id,
        $"Title {id}",
        stable,
        stable ? $"doi-{id}" : null,
        stable ? [$"work-{id}"] : [],
        [$"record-{id}"],
        new DedupSightingRef("fixture", $"trace-{id}", $"sighting-{id}", "fixture", "fixture"));

    private static string FixturePath(string name) => Environment.GetEnvironmentVariable("UPDATE_FE06_FIXTURES") == "1"
        ? Path.Combine(RepositoryRoot(), "fixtures", "conformance", "screening-corpus-binding", name)
        : Path.Combine(AppContext.BaseDirectory, "fixtures", "screening-corpus-binding", name);

    private static void WriteFixtures(IReadOnlyDictionary<string, byte[]> files)
    {
        var directory = Path.Combine(RepositoryRoot(), "fixtures", "conformance", "screening-corpus-binding");
        Directory.CreateDirectory(directory);
        foreach (var item in files)
            File.WriteAllBytes(Path.Combine(directory, item.Key), item.Value);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NexusScholar.Core.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed record Authority(
        VerifiedDeduplicationAuthorityPolicy Policy,
        VerifiedDeduplicationAuthorityResultDigest SourceResult,
        VerifiedCorpusSnapshot Snapshot);
}
