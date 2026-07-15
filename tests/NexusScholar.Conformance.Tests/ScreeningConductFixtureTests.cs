using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ScreeningConductFixtureTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Screening_conduct_fixture_catalog_has_local_contract_metadata()
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "fixtures", "conformance", "screening-conduct");
        var files = Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.AreEqual(4, files.Length);
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var value = document.RootElement;
            Assert.IsTrue(value.GetProperty("fixture_id").GetString()!.StartsWith("screening-conduct-", StringComparison.Ordinal));
            Assert.AreEqual("local-csharp-contract", value.GetProperty("scope").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(value.GetProperty("scenario").GetString()));
            CollectionAssert.Contains(value.GetProperty("non_claims").EnumerateArray().Select(item => item.GetString()).ToArray(), "no-php-compatibility-claim");
        }
    }

    [TestMethod]
    public void Screening_conduct_fixture_scenarios_execute_the_local_contract()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "fixtures", "conformance", "screening-conduct");
        foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var expected = document.RootElement.GetProperty("expected").GetString();
            var actual = expected switch
            {
                "accepted" => ExecuteSingleReview(),
                "duplicate-independent-reviewer" => ExecuteDuplicateReviewer(),
                "conflict-then-resolved" => ExecuteConflictAndAdjudication(),
                "handoff-rejected" => ExecuteInvalidation(),
                _ => throw new AssertFailedException($"Unknown Screening conduct fixture expectation '{expected}'.")
            };
            Assert.AreEqual(expected, actual, Path.GetFileName(file));
        }
    }

    private static string ExecuteSingleReview()
    {
        var (policy, header) = BuildAuthority(1);
        var decision = Review(header, 1, header.Digest, "single", "reviewer-1", ScreeningVerdicts.Include);
        var reopened = ScreeningConductCanonicalCodec.RehydrateDecision(
            ScreeningConductCanonicalCodec.Serialize(decision), decision.Digest, header);
        return ScreeningConductJournal.Rehydrate(header, policy, [reopened]).Projection.HandoffReady ? "accepted" : "rejected";
    }

    private static string ExecuteDuplicateReviewer()
    {
        var (policy, header) = BuildAuthority(2);
        var first = Review(header, 1, header.Digest, "first", "reviewer-1", ScreeningVerdicts.Include);
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [first]);
        var second = Review(header, 2, first.Digest, "second", "reviewer-1", ScreeningVerdicts.Include);
        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => journal.Append(second));
        return error.Category == ScreeningErrorCodes.DuplicateIndependentReviewer ? "duplicate-independent-reviewer" : error.Category;
    }

    private static string ExecuteConflictAndAdjudication()
    {
        var (policy, header) = BuildAuthority(2);
        var first = Review(header, 1, header.Digest, "first", "reviewer-1", ScreeningVerdicts.Include);
        var second = Review(header, 2, first.Digest, "second", "reviewer-2", ScreeningVerdicts.Exclude, "wrong-population");
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [first, second]);
        var conflict = journal.Projection.Conflicts.Single();
        var adjudication = ScreeningConductDecision.Create(
            header, 3, second.Digest, "adjudicate", "candidate-1", ScreeningConductDecisionKind.Adjudication,
            ScreeningVerdicts.Include, new ScreeningConductActor("chair-1", ScreeningConductActorKinds.Human, "chair"),
            "Resolve the disagreement.", Now, resolvedConflictId: conflict.ConflictId,
            sourceDecisionDigests: conflict.SourceDecisionDigests);
        journal.Append(adjudication);
        return journal.Projection.HandoffReady && journal.Projection.Conflicts.Single().Resolved ? "conflict-then-resolved" : "unresolved";
    }

    private static string ExecuteInvalidation()
    {
        var (policy, header) = BuildAuthority(1);
        var decision = Review(header, 1, header.Digest, "before", "reviewer-1", ScreeningVerdicts.Include);
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [decision]);
        journal.Append(ScreeningConductInvalidation.Create(
            header, 2, decision.Digest, "invalidate", new ScreeningConductEvidenceRef(
                "protocol-version", policy.ProtocolVersionId, policy.ProtocolContentDigest), [decision.Digest],
            new ScreeningConductActor("chair-1", ScreeningConductActorKinds.Human, "chair"), "Protocol changed.", Now));
        return journal.Projection.HandoffReady ? "accepted" : "handoff-rejected";
    }

    private static ScreeningConductDecision Review(
        ScreeningConductHeader header, int ordinal, ContentDigest previous, string request, string actorId,
        string verdict, string? reason = null) => ScreeningConductDecision.Create(
            header, ordinal, previous, request, "candidate-1", ScreeningConductDecisionKind.Review, verdict,
            new ScreeningConductActor(actorId, ScreeningConductActorKinds.Human, "reviewer"),
            "Fixture review rationale.", Now, reason);

    private static (ScreeningConductPolicy Policy, ScreeningConductHeader Header) BuildAuthority(int reviewCount)
    {
        var protocol = BuildProtocol();
        var candidate = new DedupCandidateRecord(
            "candidate-1", "Candidate title", true, "work:candidate-1", ["work:candidate-1"], [],
            new DedupSightingRef("search", "trace-1", "source-1", "provider"));
        var result = new DeduplicationResult(
            "dedup-fixture", "nexus.deduplication.result", "1.0.0", DeduplicationService.PolicyId,
            DeduplicationService.PolicyVersion, 0.95d,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)),
            [], [], [candidate], [], [], [], [], [], [], ["no-php-compatibility-claim"]);
        var deduplication = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(result));
        var criteria = new ScreeningCriteria(
            "criteria-fixture", "1.0.0", ScreeningStages.TitleAbstract, CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"), true, protocol.Version.Id, protocol.Version.ContentDigest.ToString(),
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());
        var policy = ScreeningConductPolicy.Create(
            "policy-fixture", "candidate-set-fixture", deduplication, protocol, criteria, reviewCount,
            [new("reviewer-1", "reviewer"), new("reviewer-2", "reviewer"), new("chair-1", "chair")],
            ["chair"], [new("wrong-population", ScreeningStages.TitleAbstract)],
            new ScreeningConductActor("chair-1", ScreeningConductActorKinds.Human, "chair"), Now);
        return (policy, ScreeningConductHeader.Create(
            "conduct-fixture", policy, new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"), Now));
    }

    private static VerifiedProtocolVersion BuildProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-fixture-v1", "protocol-fixture", "project-1", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("screening", "screen records"), new CanonicalJsonObject(), [], [], [],
            ContentDigest.Sha256Utf8("placeholder"), ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId,
            ["approval-1"], Now);
        var version = new ProtocolVersion(
            seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template, seed.Intent,
            seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers,
            seed.ToProtocolContentDigestEnvelope().ComputeDigest(), seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
