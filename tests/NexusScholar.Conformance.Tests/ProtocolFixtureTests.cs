using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ProtocolFixtureTests
{
    private static readonly string[] PositiveFixtures =
    {
        "protocol-draft-valid-v1.json",
        "protocol-approved-single-v1.json",
        "protocol-approved-dual-v1.json",
        "protocol-rehydration-approved-single-v1.json",
        "protocol-rehydration-approved-dual-v1.json",
        "protocol-amended-v1.json",
        "protocol-waiver-valid-v1.json",
        "protocol-deviation-valid-v1.json"
    };

    private static readonly string[] RequiredNegativeCategories =
    {
        "missing-required-decision",
        "blocking-unresolved-decision",
        "duplicate-decision",
        "post-approval-mutation",
        "unauthorized-approval",
        "stale-content-digest",
        "invalid-amendment",
        "invalid-waiver",
        "invalid-deviation",
        "same-actor-dual-approval",
        "automation-cannot-approve",
        "tampered-content-digest",
        "tampered-approval-digest",
        "wrong-target",
        "wrong-policy",
        "policy-downgrade",
        "missing-approval",
        "extra-approval",
        "duplicate-approval",
        "duplicate-content-identity"
    };

    private static readonly string[] RehydrationFixtures =
    {
        "protocol-rehydration-approved-single-v1.json",
        "protocol-rehydration-approved-dual-v1.json",
        "protocol-rehydration-invalid-tampered-content-digest-v1.json",
        "protocol-rehydration-invalid-tampered-approval-record-digest-v1.json",
        "protocol-rehydration-invalid-non-human-approval-v1.json",
        "protocol-rehydration-invalid-wrong-target-v1.json",
        "protocol-rehydration-invalid-wrong-policy-v1.json",
        "protocol-rehydration-invalid-missing-approvals-v1.json",
        "protocol-rehydration-invalid-extra-approvals-v1.json",
        "protocol-rehydration-invalid-duplicate-approvals-v1.json",
        "protocol-rehydration-invalid-duplicate-content-identity-v1.json",
        "protocol-rehydration-invalid-policy-downgrade-v1.json",
        "protocol-rehydration-blocking-unresolved-state-v1.json"
    };

    private static readonly string[] SupplementalAuthorityFixtures =
    {
        "protocol-supplemental-waiver-valid-v1.json",
        "protocol-supplemental-amendment-valid-v1.json",
        "protocol-supplemental-invalid-wrong-target-v1.json",
        "protocol-supplemental-invalid-non-human-v1.json",
        "protocol-supplemental-invalid-lineage-v1.json",
        "protocol-supplemental-invalid-foreign-notice-v1.json"
    };

    [TestMethod]
    public void Minimal_protocol_fixture_remains_discovery_only()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol-minimal.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.AreEqual("nexus.review-protocol/v1", root.GetProperty("schema").GetString());
        Assert.IsTrue(root.GetProperty("subject").GetString()?.Length > 0);
        Assert.AreEqual(2, root.GetProperty("required_decisions").GetArrayLength());
        Assert.AreEqual("scoping-review", root.GetProperty("decisions").GetProperty("review-type").GetString());
    }

    [TestMethod]
    public void Gate_3_protocol_fixtures_have_required_metadata()
    {
        foreach (var path in ProtocolFixturePaths())
        {
            if (Path.GetFileName(path).StartsWith("protocol-supplemental-", StringComparison.Ordinal))
            {
                continue;
            }
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            var isRehydration = root.GetProperty("fixtureId").GetString()?.StartsWith("protocol-rehydration-", StringComparison.Ordinal) == true;
            Assert.AreEqual(
                isRehydration ? "local-hardening-contract" : "local-gate-3-contract",
                root.GetProperty("sourceKind").GetString(),
                Path.GetFileName(path));
            Assert.AreEqual(
                isRehydration ? "hardening-03-v1" : "gate-3-v1",
                root.GetProperty("generatorVersion").GetString(),
                Path.GetFileName(path));
            if (isRehydration)
            {
                Assert.AreEqual(
                    "sha256 of compact JSON serialization of case",
                    root.GetProperty("digestMeaning").GetString(),
                    Path.GetFileName(path));
            }
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0003-protocol-record-contract.md", StringComparison.Ordinal)));
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0004-protocol-approval-semantics.md", StringComparison.Ordinal)));
            _ = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            _ = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Gate_3_protocol_fixtures_have_replayable_case_digests()
    {
        foreach (var path in ProtocolFixturePaths())
        {
            if (Path.GetFileName(path).StartsWith("protocol-supplemental-", StringComparison.Ordinal))
            {
                continue;
            }
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var serializedCase = JsonSerializer.Serialize(
                root.GetProperty("case"),
                new JsonSerializerOptions
                {
                    WriteIndented = false
                });
            var digest = ContentDigest.Sha256Utf8(serializedCase);

            Assert.AreEqual(
                root.GetProperty("inputDigest").GetString(),
                digest.ToString(),
                Path.GetFileName(path));
            Assert.AreEqual(
                root.GetProperty("outputDigest").GetString(),
                digest.ToString(),
                Path.GetFileName(path));
        }
    }

    [TestMethod]
    public void Gate_3_positive_fixture_pack_is_present()
    {
        var names = ProtocolFixturePaths()
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixture in PositiveFixtures)
        {
            Assert.IsTrue(names.Contains(fixture), $"Missing Gate 3 protocol fixture '{fixture}'.");
        }
    }

    [TestMethod]
    public void Rehydration_protocol_fixtures_are_present_and_tagged()
    {
        var names = ProtocolFixturePaths()
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixture in RehydrationFixtures)
        {
            Assert.IsTrue(names.Contains(fixture), $"Missing protocol rehydration fixture '{fixture}'.");

            var root = LoadProtocolFixture(fixture);
            var @case = root.GetProperty("case");
            Assert.AreEqual("protocol-rehydration", @case.GetProperty("recordType").GetString(), fixture);
            Assert.IsTrue(
                @case.GetProperty("operation").GetString()?.Length > 0,
                $"Rehydration fixture '{fixture}' must define an operation.");

            if (@case.TryGetProperty("negative", out var negative) && negative.GetBoolean())
            {
                Assert.IsTrue(
                    @case.TryGetProperty("errorCategory", out var category) &&
                    !string.IsNullOrWhiteSpace(category.GetString()),
                    $"Rehydration negative fixture '{fixture}' must define an errorCategory.");
            }
        }
    }

    [TestMethod]
    public void Rehydration_protocol_fixtures_replay_through_the_authority_boundary()
    {
        foreach (var fixture in RehydrationFixtures)
        {
            var root = LoadProtocolFixture(fixture);
            var negative = root.GetProperty("case").GetProperty("negative").GetBoolean();

            if (negative)
            {
                Assert.ThrowsExactly<ProtocolRuleException>(
                    () => ReplayRehydrationFixture(root.GetProperty("case")),
                    fixture);
            }
            else
            {
                var verified = ReplayRehydrationFixture(root.GetProperty("case"));
                Assert.AreEqual(ProtocolStatus.Approved, verified!.Version.Status, fixture);
                Assert.AreEqual(
                    verified.Version.ContentDigest,
                    verified.Version.ToProtocolContentDigestEnvelope().ComputeDigest(),
                    fixture);
            }
        }
    }

    [TestMethod]
    public void Rehydration_protocol_superseded_status_is_replayed()
    {
        using var document = JsonDocument.Parse("{\"policyMode\":\"single_researcher\",\"mutation\":\"superseded_status\"}");
        var verified = ReplayRehydrationFixture(document.RootElement);

        Assert.IsNotNull(verified);
        Assert.AreEqual(ProtocolStatus.Superseded, verified.Version.Status);
        Assert.AreEqual("protocol-version-superseding", verified.Version.SupersededByVersionId);
    }

    [TestMethod]
    public void Supplemental_authority_recipes_are_complete_and_digest_replayable()
    {
        var names = ProtocolFixturePaths().Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var mutations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fixture in SupplementalAuthorityFixtures)
        {
            Assert.IsTrue(names.Contains(fixture), $"Missing supplemental authority recipe '{fixture}'.");
            var root = LoadProtocolFixture(fixture);
            Assert.AreEqual("local-hardening-contract", root.GetProperty("sourceKind").GetString(), fixture);
            Assert.AreEqual("hardening-05-v1", root.GetProperty("generatorVersion").GetString(), fixture);
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0019-protocol-supplemental-authority-records.md", StringComparison.Ordinal)), fixture);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)), fixture);

            var @case = root.GetProperty("case");
            var compact = JsonSerializer.Serialize(@case, new JsonSerializerOptions { WriteIndented = false });
            var digest = ContentDigest.Sha256Utf8(compact).ToString();
            Assert.AreEqual(root.GetProperty("inputDigest").GetString(), digest, fixture);
            Assert.AreEqual(root.GetProperty("outputDigest").GetString(), digest, fixture);
            operations.Add(@case.GetProperty("operation").GetString()!);
            mutations.Add(@case.GetProperty("mutation").GetString()!);
        }

        CollectionAssert.AreEquivalent(new[] { "rehydrate-waiver", "rehydrate-amendment" }, operations.ToArray());
        foreach (var mutation in new[] { "none", "wrong-target", "non-human", "wrong-lineage", "foreign-notice" })
        {
            Assert.IsTrue(mutations.Contains(mutation), $"Missing supplemental authority mutation recipe '{mutation}'.");
        }
    }

    [TestMethod]
    public void Approved_protocol_fixtures_separate_content_and_approval_digest_scopes()
    {
        foreach (var fixture in new[] { "protocol-approved-single-v1.json", "protocol-approved-dual-v1.json" })
        {
            var root = LoadProtocolFixture(fixture);
            var version = root.GetProperty("case").GetProperty("protocol_version");

            Assert.AreEqual("protocol-content", version.GetProperty("content_digest").GetProperty("scope").GetString());
            Assert.AreEqual("approval-record", version.GetProperty("approval_records")[0].GetProperty("approval_record_digest").GetProperty("scope").GetString());
            Assert.IsFalse(version.GetProperty("digest_material_excludes").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "protocol-content", StringComparison.Ordinal)));
            Assert.IsTrue(version.GetProperty("digest_material_excludes").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "approval_records", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Negative_protocol_fixtures_cover_required_error_categories()
    {
        var categories = ProtocolFixturePaths()
            .Select(Load)
            .Where(root => root.GetProperty("case").TryGetProperty("negative", out var negative) && negative.GetBoolean())
            .Select(root => root.GetProperty("case").GetProperty("errorCategory").GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var category in RequiredNegativeCategories)
        {
            Assert.IsTrue(categories.Contains(category), $"Missing negative fixture for '{category}'.");
        }
    }

    [TestMethod]
    public void Key_value_digest_material_is_explicitly_rejected_by_fixture()
    {
        var root = LoadProtocolFixture("protocol-invalid-key-value-digest-material-v1.json");
        var rejected = root.GetProperty("case").GetProperty("rejectedDigestMaterial");

        Assert.AreEqual("key=value-lines", rejected.GetProperty("format").GetString());
        Assert.AreEqual("stale-content-digest", root.GetProperty("case").GetProperty("errorCategory").GetString());
    }

    private static JsonElement LoadProtocolFixture(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol", filename);
        return Load(path);
    }

    private static JsonElement Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string[] ProtocolFixturePaths()
    {
        return Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol"), "*.json");
    }

    private static VerifiedProtocolVersion? ReplayRehydrationFixture(JsonElement fixtureCase)
    {
        var policyMode = fixtureCase.GetProperty("policyMode").GetString();
        var mutation = fixtureCase.GetProperty("mutation").GetString();
        var dual = string.Equals(policyMode, "dual_independent", StringComparison.Ordinal);
        var policy = dual ? ApprovalPolicy.DualIndependent() : ApprovalPolicy.ExplicitCustomSingleResearcher();
        var state = CreateReplayState(policy, string.Equals(mutation, "extra_approvals", StringComparison.Ordinal));
        var input = ToUnverified(state.Version, policy);

        if (string.Equals(mutation, "tampered_content", StringComparison.Ordinal))
        {
            input = input with { Values = new CanonicalJsonObject().Add("review_family", "tampered") };
        }
        else if (string.Equals(mutation, "duplicate_content_identity", StringComparison.Ordinal))
        {
            input = input with { Decisions = input.Decisions.Concat(new[] { input.Decisions[0] }).ToArray() };
        }
        else if (string.Equals(mutation, "missing_approvals", StringComparison.Ordinal))
        {
            input = input with { ApprovalIds = Array.Empty<string>() };
        }
        else if (string.Equals(mutation, "duplicate_approvals", StringComparison.Ordinal))
        {
            input = input with { ApprovalIds = new[] { input.ApprovalIds[0], input.ApprovalIds[0] } };
        }
        else if (string.Equals(mutation, "blocking_unresolved", StringComparison.Ordinal))
        {
            input = input with
            {
                UnresolvedDecisions = new[]
                {
                    new UnresolvedDecision(
                        "unresolved-1",
                        "review-type",
                        "Unresolved question",
                        "Fixture replay",
                        "protocol-approval",
                        ReplayResearcher.Id,
                        ReplayClock.UtcNow,
                        true)
                }
            };
        }
        else if (string.Equals(mutation, "policy_downgrade", StringComparison.Ordinal))
        {
            var claimed = ApprovalPolicy.DualIndependent();
            input = input with { ApprovalPolicy = claimed };
            state = state with
            {
                Resolver = state.Resolver.WithPolicy(new ApprovalPolicy(
                    claimed.PolicyId,
                    claimed.PolicyVersion,
                    ApprovalPolicyMode.SingleResearcher,
                    Array.Empty<string>(),
                    1,
                    false,
                    false))
            };
        }
        else if (mutation is "tampered_approval" or "wrong_target" or "wrong_policy" or "non_human_actor")
        {
            var approvalInput = ToUnverified(state.RawApprovals[0], policy);
            if (string.Equals(mutation, "tampered_approval", StringComparison.Ordinal))
            {
                approvalInput = approvalInput with { Rationale = "tampered" };
            }
            else if (string.Equals(mutation, "wrong_target", StringComparison.Ordinal))
            {
                approvalInput = approvalInput with { TargetId = "wrong-version" };
            }
            else if (string.Equals(mutation, "wrong_policy", StringComparison.Ordinal))
            {
                approvalInput = approvalInput with { PolicyId = "wrong-policy" };
            }

            var resolver = string.Equals(mutation, "non_human_actor", StringComparison.Ordinal)
                ? new ReplayResolver(policy, Array.Empty<ActorId>())
                : new ReplayResolver(policy, new[] { ReplayResearcher.Id, ReplayReviewer.Id });
            _ = ProtocolRehydrator.RehydrateApproval(approvalInput, state.Candidate, policy, resolver);
            Assert.Fail($"Negative approval fixture mutation '{mutation}' unexpectedly rehydrated.");
        }
        else if (string.Equals(mutation, "superseded_status", StringComparison.Ordinal))
        {
            input = input with
            {
                Status = ProtocolStatus.Superseded,
                SupersededByVersionId = "protocol-version-superseding"
            };
        }

        return ProtocolRehydrator.RehydrateVersion(input, state.Resolver);
    }

    private static ReplayState CreateReplayState(ApprovalPolicy policy, bool extraApproval)
    {
        var ids = new ReplayIds();
        var draft = ProtocolDraft.Create(
            ids,
            "project-replay",
            new ProtocolTemplate("template-replay", "1.0.0", ContentDigest.Sha256Utf8("template-replay")),
            new ProtocolIntent("fixture replay", "verify authority rehydration"),
            new CanonicalJsonObject().Add("review_family", "custom"),
            new[]
            {
                new RequiredDecisionDefinition(
                    "review-type",
                    "Review type",
                    "Select review type",
                    new CanonicalJsonObject().Add("type", "string"),
                    "protocol-approval",
                    "protocol-approval",
                    "review-type",
                    false)
            },
            ReplayResearcher,
            ReplayClock);
        draft.RecordDecision(ids, "review-type", CanonicalJsonValue.From("custom"), ReplayResearcher, ReplayClock);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var actors = policy.Mode == ApprovalPolicyMode.DualIndependent || extraApproval
            ? new[] { ReplayResearcher, ReplayReviewer }
            : new[] { ReplayResearcher };
        var resolver = new ReplayResolver(policy, actors.Select(actor => actor.Id));
        var rawApprovals = actors
            .Select(actor => ProtocolApproval.Create(ids, candidate, policy, actor, ReplayClock, candidate.ContentDigest))
            .ToArray();
        var verifiedApprovals = rawApprovals
            .Select(approval => ProtocolRehydrator.RehydrateApproval(ToUnverified(approval, policy), candidate, policy, resolver))
            .ToArray();
        resolver = resolver.WithApprovals(verifiedApprovals);
        var version = draft.ApproveCandidate(candidate, policy, rawApprovals, ReplayClock);
        return new ReplayState(candidate, version, rawApprovals, resolver);
    }

    private static UnverifiedProtocolApproval ToUnverified(ProtocolApproval approval, ApprovalPolicy policy) =>
        new(
            approval.ApprovalId,
            approval.TargetType,
            approval.TargetId,
            approval.ProtocolId,
            approval.ProtocolVersionId,
            approval.ProtocolVersionNumber,
            approval.ContentDigest,
            approval.PolicyId,
            approval.PolicyVersion,
            policy.Mode,
            approval.Decision,
            approval.ApprovedBy,
            approval.ApprovedAt,
            approval.Role,
            approval.Rationale,
            approval.SupersedesApprovalId,
            approval.ApprovalRecordDigest);

    private static UnverifiedProtocolVersion ToUnverified(ProtocolVersion version, ApprovalPolicy policy) =>
        new(
            version.Id,
            version.ProtocolId,
            version.ProjectId,
            version.VersionNumber,
            version.Status,
            version.Template,
            version.Intent,
            version.Values,
            version.RequiredDecisions,
            version.Decisions,
            version.Waivers,
            version.UnresolvedDecisions,
            version.ContentDigest,
            policy,
            version.ApprovalIds,
            version.ApprovedAt!.Value,
            version.SupersedesVersionId,
            version.SupersededByVersionId,
            version.AmendmentId);

    private static readonly ProtocolActor ReplayResearcher = ProtocolActor.Human("fixture-researcher");
    private static readonly ProtocolActor ReplayReviewer = ProtocolActor.Human("fixture-reviewer");
    private static readonly IClock ReplayClock = new ReplayFixedClock();

    private sealed record ReplayState(
        ProtocolVersion Candidate,
        ProtocolVersion Version,
        IReadOnlyList<ProtocolApproval> RawApprovals,
        ReplayResolver Resolver);

    private sealed class ReplayResolver : IProtocolAuthorityResolver
    {
        private readonly HashSet<ActorId> _actors;
        private readonly IReadOnlyDictionary<string, VerifiedProtocolApproval> _approvals;

        public ReplayResolver(
            ApprovalPolicy policy,
            IEnumerable<ActorId> actors,
            IEnumerable<VerifiedProtocolApproval>? approvals = null)
        {
            Policy = policy;
            _actors = actors.ToHashSet();
            _approvals = (approvals ?? Array.Empty<VerifiedProtocolApproval>())
                .ToDictionary(item => item.Approval.ApprovalId, StringComparer.Ordinal);
        }

        public ApprovalPolicy Policy { get; }

        public ApprovalPolicy ResolveApprovalPolicy(ProtocolTemplate template) => Policy;

        public bool IsHumanActor(ActorId actorId) => _actors.Contains(actorId);

        public VerifiedProtocolApproval ResolveApproval(string approvalId) =>
            _approvals.TryGetValue(approvalId, out var approval) ? approval : null!;

        public ReplayResolver WithPolicy(ApprovalPolicy policy) => new(policy, _actors, _approvals.Values);

        public ReplayResolver WithApprovals(IEnumerable<VerifiedProtocolApproval> approvals) =>
            new(Policy, _actors, approvals);
    }

    private sealed class ReplayIds : IIdGenerator
    {
        private int _next = 1;

        public Guid NewId() => new(_next++, 0, 0, new byte[8]);
    }

    private sealed class ReplayFixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    }
}
