using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ScreeningCriteriaCanonicalCodecTests
{
    [TestMethod]
    public void Canonical_round_trip_preserves_all_optional_fields_and_title_abstract_binding()
    {
        var protocol = BuildProtocol();
        var expected = new ScreeningCriteria(
            "criteria-canonical-roundtrip",
            "1.0.0",
            ScreeningStages.TitleAbstract,
            new CanonicalJsonObject().Add("type", "inclusion").Add("threshold", 0.7),
            new CanonicalJsonObject().Add("type", "exclusion").Add("threshold", 0.2),
            requiresProtocolBinding: true,
            protocol.Version.Id,
            protocol.Version.ContentDigest.ToString(),
            "workflow-canonical",
            "Prioritize title and abstract signal.",
            new CanonicalJsonObject().Add("max_chars", 1200),
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

        var bytes = ScreeningCriteriaCanonicalCodec.Serialize(expected);
        var actual = ScreeningCriteriaCanonicalCodec.Rehydrate(bytes, expected.ComputeDigest(), protocol);

        Assert.AreEqual(expected.CriteriaId, actual.CriteriaId);
        Assert.AreEqual(expected.CriteriaVersion, actual.CriteriaVersion);
        Assert.AreEqual(expected.Stage, actual.Stage);
        Assert.AreEqual(ScreeningStages.TitleAbstract, actual.Stage);
        Assert.AreEqual("workflow-canonical", actual.WorkflowBinding);
        Assert.AreEqual("Prioritize title and abstract signal.", actual.ReviewGuidance);
        Assert.AreEqual(actual.FullTextRequirements?.ToString(), expected.FullTextRequirements?.ToString());
        Assert.IsTrue(bytes.SequenceEqual(ScreeningCriteriaCanonicalCodec.Serialize(actual)));
    }

    [TestMethod]
    public void Canonical_codec_rejects_unknown_stage_digest_and_protocol_mismatches()
    {
        var protocol = BuildProtocol();
        var criteria = BuildTitleAbstractCriteria(protocol);
        var bytes = ScreeningCriteriaCanonicalCodec.Serialize(criteria);

        var wrongStage = Mutate(bytes, root => root["stage"] = "full_text");
        AssertInvalid(() => ScreeningCriteriaCanonicalCodec.Rehydrate(wrongStage, criteria.ComputeDigest(), protocol),
            ScreeningErrorCodes.InvalidCriteriaCanonicalRecord);

        var wrongProtocol = Mutate(bytes, root => root["approved_protocol_binding"] = "protocol-other");
        AssertInvalid(() => ScreeningCriteriaCanonicalCodec.Rehydrate(wrongProtocol, criteria.ComputeDigest(), protocol),
            ScreeningErrorCodes.InvalidProtocolBinding);
    }

    [TestMethod]
    public void Canonical_codec_rejects_noncanonical_and_unknown_bytes()
    {
        var protocol = BuildProtocol();
        var criteria = BuildTitleAbstractCriteria(protocol);
        var bytes = ScreeningCriteriaCanonicalCodec.Serialize(criteria);

        var nonCanonical = bytes.Concat([(byte)'\n']).ToArray();
        AssertInvalid(() => ScreeningCriteriaCanonicalCodec.Rehydrate(nonCanonical, criteria.ComputeDigest(), protocol),
            ScreeningErrorCodes.InvalidCriteriaCanonicalRecord);

        var unknown = Mutate(bytes, root => root["unknown"] = "field");
        AssertInvalid(() => ScreeningCriteriaCanonicalCodec.Rehydrate(unknown, criteria.ComputeDigest(), protocol),
            ScreeningErrorCodes.InvalidCriteriaCanonicalRecord);
    }

    [TestMethod]
    public void Canonical_codec_rejects_tampered_bindings_and_digest_mismatches()
    {
        var protocol = BuildProtocol();
        var criteria = BuildTitleAbstractCriteria(protocol);
        var bytes = ScreeningCriteriaCanonicalCodec.Serialize(criteria);
        var expected = criteria.ComputeDigest();

        var wrongCurrent = Mutate(bytes, root =>
            root["current_protocol_content_digest"] = ContentDigest.Sha256Utf8("different-protocol").ToString());
        AssertInvalid(() => ScreeningCriteriaCanonicalCodec.Rehydrate(wrongCurrent, criteria.ComputeDigest(), protocol),
            ScreeningErrorCodes.InvalidProtocolBinding);

        var wrongExpected = ContentDigest.Sha256Utf8("different");
        AssertInvalid(() => ScreeningCriteriaCanonicalCodec.Rehydrate(bytes, wrongExpected, protocol),
            ScreeningErrorCodes.CriteriaDigestMismatch);
    }

    private static VerifiedProtocolVersion BuildProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-screening-canonical",
            "protocol-screening",
            "project-screening",
            1,
            ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("screening-template")),
            new ProtocolIntent("screening", "criteria binding"),
            new CanonicalJsonObject().Add("scope", "screening"),
            [],
            [],
            [],
            ContentDigest.Sha256Utf8("template"),
            ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId,
            ["approval-criteria-canonical"],
            DateTimeOffset.UtcNow);

        var version = new ProtocolVersion(
            seed.Id,
            seed.ProtocolId,
            seed.ProjectId,
            seed.VersionNumber,
            seed.Status,
            seed.Template,
            seed.Intent,
            seed.Values,
            seed.RequiredDecisions,
            seed.Decisions,
            seed.Waivers,
            seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId,
            seed.ApprovalIds,
            seed.ApprovedAt,
            seed.SupersedesVersionId,
            seed.SupersededByVersionId,
            seed.AmendmentId,
            seed.UnresolvedDecisions);

        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private static ScreeningCriteria BuildTitleAbstractCriteria(VerifiedProtocolVersion protocol)
    {
        return new ScreeningCriteria(
            "criteria-title-abstract",
            "1.0.0",
            ScreeningStages.TitleAbstract,
            CanonicalJsonValue.From("include"),
            CanonicalJsonValue.From("exclude"),
            requiresProtocolBinding: true,
            protocol.Version.Id,
            protocol.Version.ContentDigest.ToString(),
            approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
            approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
            currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());
    }

    private static void AssertInvalid(Action action, string category)
    {
        var error = Assert.ThrowsExactly<ScreeningRuleException>(action);
        Assert.AreEqual(category, error.Category);
    }

    private static byte[] Mutate(byte[] bytes, Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(bytes)!.AsObject();
        mutation(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
    }
}
