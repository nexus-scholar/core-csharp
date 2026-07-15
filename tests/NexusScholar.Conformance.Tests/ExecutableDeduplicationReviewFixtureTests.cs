using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ExecutableDeduplicationReviewFixtureTests
{
    private static readonly IClock Clock = new FixedClock();

    [TestMethod]
    public void ExecutableDeduplicationReview_fixture_replays_byte_identically()
    {
        var fixture = Build();
        if (Environment.GetEnvironmentVariable("UPDATE_FE02_FIXTURES") == "1") Write(fixture.Files);
        foreach (var file in fixture.Files)
            CollectionAssert.AreEqual(File.ReadAllBytes(PathFor(file.Key)), file.Value, file.Key);

        var command = ResearchWorkspaceAuthorityArtifacts.VerifyReviewCommandCanonicalRecord(
            fixture.Files["review-command.json"], fixture.Policy, fixture.Source, fixture.Target,
            fixture.Baseline.DecisionSetDigest, "authority-fe02-baseline", ContentDigest.Sha256Utf8("authority-manifest"),
            fixture.Baseline.SnapshotId, fixture.Baseline.RecordDigest);
        var decision = ResearchWorkspaceAuthorityArtifacts.VerifyDecisionCanonicalRecord(
            fixture.Files["decision.json"], fixture.Policy, fixture.Source, fixture.Target);
        var successor = ResearchWorkspaceAuthorityArtifacts.VerifySuccessorSnapshotCanonicalRecord(
            fixture.Files["successor-snapshot.json"], fixture.Source, fixture.Policy, fixture.Baseline,
            new[] { decision }, new[] { decision }, new[] { fixture.Baseline }, decision);
        _ = ResearchWorkspaceAuthorityArtifacts.VerifyInvalidationCanonicalRecord(
            fixture.Files["invalidation.json"], fixture.Policy, decision, successor,
            new[] { decision }, new[] { fixture.Baseline, successor });
        Assert.AreEqual(command.DecisionId, decision.DecisionId);
    }

    [TestMethod]
    public void ExecutableDeduplicationReview_tampered_request_digest_is_rejected()
    {
        var fixture = Build();
        var recorded = fixture.Command.RequestDigest.ToString();
        var tampered = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(fixture.Files["review-command.json"])
            .Replace(recorded, ContentDigest.Sha256Utf8("tampered").ToString(), StringComparison.Ordinal));

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            ResearchWorkspaceAuthorityArtifacts.VerifyReviewCommandCanonicalRecord(
                tampered, fixture.Policy, fixture.Source, fixture.Target, fixture.Baseline.DecisionSetDigest,
                "authority-fe02-baseline", ContentDigest.Sha256Utf8("authority-manifest"),
                fixture.Baseline.SnapshotId, fixture.Baseline.RecordDigest));
        Assert.AreEqual(DeduplicationReviewCommandErrorCodes.InvalidCommand, error.Category);
    }

    private static Fixture Build()
    {
        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
            DeduplicationAuthorityPolicyConstants.SchemaId, DeduplicationAuthorityPolicyConstants.SchemaVersion,
            DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, DeduplicationService.PolicyId, DeduplicationService.PolicyVersion,
            new[] { new DeduplicationAuthorityPolicyActorRole("alice", "owner") }, DeduplicationAuthorityPolicyConstants.ClosedActions,
            new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup("merge", new[] { "duplicate" }),
                new DeduplicationAuthorityPolicyReasonGroup("keep-separate", new[] { "different" }),
                new DeduplicationAuthorityPolicyReasonGroup("mark-unresolved", new[] { "uncertain" })
            }, false, "alice", "owner", Clock.UtcNow));
        var a = Candidate("candidate-a", "Shared title");
        var b = Candidate("candidate-b", "Shared title copy");
        var evidence = new DedupEvidence("evidence-fe02", DedupEvidenceKind.FuzzyTitle, a.CandidateId, b.CandidateId,
            "similar", true, .96, DeduplicationService.PolicyId, DeduplicationService.PolicyVersion);
        var pair = new DedupReviewCandidate(a.CandidateId, b.CandidateId, .96, .95);
        var result = new DeduplicationResult("result-fe02-fixture", DeduplicationService.ResultSchemaId, DeduplicationService.ResultSchemaVersion,
            DeduplicationService.PolicyId, DeduplicationService.PolicyVersion, .95, DeduplicationService.DefaultProviderPriority,
            Array.Empty<string>(), Array.Empty<string>(), new[] { a, b }, Array.Empty<DedupCluster>(), new[] { evidence },
            new[] { a, b }, new[] { pair }, Array.Empty<DedupMessage>(), Array.Empty<DedupMessage>(), Array.Empty<string>());
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
        var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(source, pair,
            new[] { a.CandidateId, b.CandidateId }, new[] { evidence });
        var baseline = CorpusSnapshotService.CreateBaseline("snapshot-fe02-baseline", source, policy, "alice", "owner", Clock);
        var command = DeduplicationReviewCommand.Create(new UnverifiedDeduplicationReviewCommand(
            DeduplicationReviewCommandConstants.SchemaId, DeduplicationReviewCommandConstants.SchemaVersion,
            "authority-fe02-baseline", ContentDigest.Sha256Utf8("authority-manifest"), baseline.DecisionSetDigest,
            source.Result.ResultId, source.ResultDigest, baseline.SnapshotId, baseline.RecordDigest,
            target.TargetKind, target.TargetId, target.TargetDigest, policy.PolicyId, policy.PolicyVersion, policy.PolicyDigest,
            "merge", "duplicate", "Human reviewed duplicate evidence.", "alice", "owner", null, null),
            policy, source, target, baseline.DecisionSetDigest, "authority-fe02-baseline",
            ContentDigest.Sha256Utf8("authority-manifest"), baseline.SnapshotId, baseline.RecordDigest);
        var decision = DeduplicationDecision.CreateDecisionMaterial(
            DeduplicationReviewCommand.BuildDecisionMaterial(command, target), Clock, policy, source, target);
        var references = decision.InvalidationEffects.Select(item => new CorpusSnapshotInvalidationReference(
            item.RecordKind, item.RecordId, item.RecordDigest)).ToArray();
        var successor = CorpusSnapshotService.CreateSuccessor("snapshot-fe02-successor", baseline, policy, "alice", "owner", Clock,
            new[] { decision }, references, new[] { decision }, new[] { baseline }, source, decision);
        var invalidation = CorpusSnapshotInvalidation.CreateInvalidationMaterial(new UnverifiedCorpusSnapshotInvalidation(
            CorpusSnapshotInvalidationConstants.SchemaId, CorpusSnapshotInvalidationConstants.SchemaVersion,
            "invalidation-fe02", decision.DecisionId, decision.DecisionDigest, successor.SnapshotId, successor.RecordDigest,
            decision.InvalidationEffects.Select(item => new CorpusSnapshotInvalidationInvalidatedRecordReference(
                item.RecordKind, item.RecordId, item.RecordDigest)).ToArray(), "alice", "owner", policy.PolicyId,
            DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, policy.PolicyDigest, Clock.UtcNow),
            Clock, policy, decision, successor, new[] { decision }, new[] { baseline, successor });
        var records = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["review-command.json"] = ResearchWorkspaceAuthorityArtifacts.SerializeReviewCommandCanonicalRecord(command),
            ["decision.json"] = ResearchWorkspaceAuthorityArtifacts.SerializeDecisionCanonicalRecord(decision),
            ["successor-snapshot.json"] = ResearchWorkspaceAuthorityArtifacts.SerializeSnapshotCanonicalRecord(successor),
            ["invalidation.json"] = ResearchWorkspaceAuthorityArtifacts.SerializeInvalidationCanonicalRecord(invalidation)
        };
        records["manifest.json"] = CanonicalJsonSerializer.SerializeToUtf8Bytes(new CanonicalJsonObject()
            .Add("schema", "nexus.fe02.local-conformance.v1")
            .Add("source", "ADR 0029 local C# contract; no PHP compatibility claim")
            .Add("files", CanonicalJsonValue.Array(records.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
                (CanonicalJsonValue)new CanonicalJsonObject().Add("name", item.Key).Add("sha256", ContentDigest.Sha256(item.Value).ToString())).ToArray())));
        return new Fixture(policy, source, target, baseline, command, records);
    }

    private static DedupCandidateRecord Candidate(string id, string title) => new(
        id, title, false, null, Array.Empty<string>(), new[] { $"record-{id}" },
        new DedupSightingRef("fixture", $"trace-{id}", $"sighting-{id}"));

    private static string PathFor(string name) => Environment.GetEnvironmentVariable("UPDATE_FE02_FIXTURES") == "1"
        ? Path.Combine(Root(), "fixtures", "conformance", "executable-deduplication-review", name)
        : Path.Combine(AppContext.BaseDirectory, "fixtures", "executable-deduplication-review", name);

    private static void Write(IReadOnlyDictionary<string, byte[]> files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFor("manifest.json"))!);
        foreach (var file in files) File.WriteAllBytes(PathFor(file.Key), file.Value);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NexusScholar.Core.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed record Fixture(VerifiedDeduplicationAuthorityPolicy Policy,
        VerifiedDeduplicationAuthorityResultDigest Source, VerifiedDeduplicationAuthorityReviewTargetDigest Target,
        VerifiedCorpusSnapshot Baseline, VerifiedDeduplicationReviewCommand Command, Dictionary<string, byte[]> Files);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 7, 15, 16, 0, 0, TimeSpan.Zero); }
}
