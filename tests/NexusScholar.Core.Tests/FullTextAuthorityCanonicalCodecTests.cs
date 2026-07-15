using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.FullText;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class FullTextAuthorityCanonicalCodecTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 7, 15, 9, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void Authority_records_round_trip_all_semantic_fields_and_preserve_order()
    {
        var (input, acquisition, artifact, rawBytes) = BuildAuthority();
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(artifact);

        var verified = Rehydrate(inputBytes, acquisitionBytes, artifactBytes, rawBytes);

        Assert.AreEqual(input.InputId, verified.Input.InputId);
        CollectionAssert.AreEqual(input.SourceRefs.ToArray(), verified.Input.SourceRefs.ToArray());
        CollectionAssert.AreEqual(
            new[] { "attempt-1", "attempt-2" },
            verified.Acquisition.SourceAttempts.Select(attempt => attempt.AttemptId).ToArray());
        CollectionAssert.AreEqual(new[] { "warning-a", "warning-b" }, verified.Artifact.Warnings.ToArray());
        Assert.AreEqual("fulltext/source.txt", verified.Artifact.LogicalPath);
        Assert.IsFalse(Encoding.UTF8.GetString(artifactBytes).Contains("canonical full text", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Authority_records_preserve_omission_and_reject_null_drift()
    {
        var (input, acquisition, artifact, rawBytes) = BuildAuthority(includeOptionalFields: false);
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(artifact);
        Assert.IsFalse(Encoding.UTF8.GetString(inputBytes).Contains("work_id", StringComparison.Ordinal));
        Assert.IsFalse(Encoding.UTF8.GetString(acquisitionBytes).Contains("source_url", StringComparison.Ordinal));
        Assert.IsFalse(Encoding.UTF8.GetString(artifactBytes).Contains("logical_path", StringComparison.Ordinal));
        _ = Rehydrate(inputBytes, acquisitionBytes, artifactBytes, rawBytes);

        var nullDrift = Mutate(inputBytes, root => root["content"]!["work_id"] = null);
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            nullDrift, Digest(nullDrift), acquisitionBytes, Digest(acquisitionBytes),
            artifactBytes, Digest(artifactBytes), rawBytes, 4096));
    }

    [TestMethod]
    public void Rehydration_rejects_unknown_missing_noncanonical_and_wrong_digest_records()
    {
        var (input, acquisition, artifact, rawBytes) = BuildAuthority();
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(artifact);

        var unknown = Mutate(acquisitionBytes, root => root["content"]!["unknown"] = "value");
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), unknown, Digest(unknown), artifactBytes, Digest(artifactBytes), rawBytes, 4096));

        var missing = Mutate(artifactBytes, root => ((JsonObject)root["content"]!).Remove("media_type"));
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), acquisitionBytes, Digest(acquisitionBytes), missing, Digest(missing), rawBytes, 4096));

        var noncanonical = inputBytes.Concat([(byte)'\n']).ToArray();
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            noncanonical, Digest(inputBytes), acquisitionBytes, Digest(acquisitionBytes), artifactBytes, Digest(artifactBytes), rawBytes, 4096));

        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(acquisitionBytes), acquisitionBytes, Digest(acquisitionBytes), artifactBytes, Digest(artifactBytes), rawBytes, 4096));
    }

    [TestMethod]
    public void Rehydration_rejects_altered_authority_bindings()
    {
        var (input, acquisition, artifact, rawBytes) = BuildAuthority();
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(artifact);

        var alteredInput = Mutate(acquisitionBytes, root => root["content"]!["input_ref"]!["candidate_id"] = "candidate-splice");
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), alteredInput, Digest(alteredInput), artifactBytes, Digest(artifactBytes), rawBytes, 4096));

        var alteredAcquisition = Mutate(artifactBytes, root => root["content"]!["acquisition_id"] = "acquisition-splice");
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), acquisitionBytes, Digest(acquisitionBytes), alteredAcquisition, Digest(alteredAcquisition), rawBytes, 4096));
    }

    [TestMethod]
    public void Rehydration_rejects_attempt_reorder_and_gap()
    {
        var (input, acquisition, artifact, rawBytes) = BuildAuthority();
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(artifact);

        var reordered = Mutate(acquisitionBytes, root =>
        {
            var attempts = (JsonArray)root["content"]!["source_attempts"]!;
            var first = attempts[0]!.DeepClone();
            var second = attempts[1]!.DeepClone();
            attempts[0] = second;
            attempts[1] = first;
        });
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), reordered, Digest(reordered), artifactBytes, Digest(artifactBytes), rawBytes, 4096));

        var gap = Mutate(acquisitionBytes, root => root["content"]!["source_attempts"]![1]!["attempt_order"] = 3);
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), gap, Digest(gap), artifactBytes, Digest(artifactBytes), rawBytes, 4096));
    }

    [TestMethod]
    public void Rehydration_rejects_raw_byte_tamper_and_maximum_size_violation()
    {
        var (input, acquisition, artifact, rawBytes) = BuildAuthority();
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(artifact);

        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), acquisitionBytes, Digest(acquisitionBytes), artifactBytes, Digest(artifactBytes),
            Encoding.UTF8.GetBytes("tampered raw bytes"), 4096), FullTextErrorCodes.RawArtifactDigestMismatch);
        AssertInvalid(() => FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), acquisitionBytes, Digest(acquisitionBytes), artifactBytes, Digest(artifactBytes),
            rawBytes, rawBytes.Length - 1), FullTextErrorCodes.ArtifactTooLarge);
    }

    private static VerifiedFullTextChain Rehydrate(
        byte[] inputBytes, byte[] acquisitionBytes, byte[] artifactBytes, byte[] rawBytes) =>
        FullTextAuthorityCanonicalCodec.Rehydrate(
            inputBytes, Digest(inputBytes), acquisitionBytes, Digest(acquisitionBytes),
            artifactBytes, Digest(artifactBytes), rawBytes, 4096);

    private static (FullTextInput Input, FullTextAcquisitionRecord Acquisition, FullTextArtifactEvidence Artifact, byte[] RawBytes)
        BuildAuthority(bool includeOptionalFields = true)
    {
        var input = new FullTextInput(
            "input-1", FullTextSourceKinds.ScreeningHandoff, "candidate-set-1", "candidate-1",
            FullTextEligibility.Retrievable,
            [new FullTextSourceRef("screening-handoff", "handoff-1"), new FullTextSourceRef("decision", "decision-1")],
            includeOptionalFields ? "decision-1" : null,
            includeOptionalFields ? "title_abstract" : null,
            includeOptionalFields ? "dedup-1" : null,
            includeOptionalFields ? "cluster-1" : null,
            includeOptionalFields ? "doi:10.1000/example" : null,
            ["non-claim-b", "non-claim-a"]);
        var attempts = new[]
        {
            new FullTextSourceAttempt(
                "attempt-1", "repository", 1, FullTextAcquisitionKinds.OpenAccessSourceReference,
                FullTextAttemptStatuses.Failure, sourceUrl: includeOptionalFields ? "https://example.test/item" : null,
                sourceReference: includeOptionalFields ? "repository-record" : null, httpStatus: includeOptionalFields ? 404 : null,
                errorCategory: includeOptionalFields ? "not-found" : null, errorMessage: includeOptionalFields ? "missing" : null,
                sourceMetadata: new Dictionary<string, string> { ["z"] = "last", ["a"] = "first" }),
            new FullTextSourceAttempt(
                "attempt-2", "manual", 2, FullTextAcquisitionKinds.ManualAcquisition,
                FullTextAttemptStatuses.Success, artifactKind: includeOptionalFields ? FullTextArtifactKinds.Text : null,
                mediaType: includeOptionalFields ? "text/plain" : null, artifactEvidenceId: includeOptionalFields ? "artifact-1" : null)
        };
        var acquisition = new FullTextAcquisitionRecord(
            "acquisition-1", input, FullTextAcquisitionKinds.ManualAcquisition, "manual", "operator-supplied-bytes",
            new FullTextActor("human-1", FullTextActorKinds.Human), FixedTime, FullTextAttemptStatuses.Success, attempts,
            sourceUrl: includeOptionalFields ? "https://example.test/source" : null,
            doiOrLandingPage: includeOptionalFields ? "doi:10.1000/example" : null,
            sourceMetadata: new Dictionary<string, string> { ["access"] = "supplied", ["legal_status"] = "not-assessed" },
            artifactEvidenceId: includeOptionalFields ? "artifact-1" : null,
            warnings: ["acquisition-warning"], errors: [], nonClaims: ["no-legality-certification"]);
        var rawBytes = Encoding.UTF8.GetBytes("canonical full text");
        var artifact = new FullTextArtifactEvidence(
            "artifact-1", input, input.CandidateId, acquisition.AcquisitionId, acquisition.AcquisitionKind,
            acquisition.SourceAlias, FullTextArtifactKinds.Text, "text/plain", rawBytes.LongLength,
            ContentDigest.Sha256(rawBytes).ToString(), DigestScope.RawArtifactBytes.ToString(), FullTextAttemptStatuses.Success,
            rawBytes,
            input.CandidateSetId,
            includeOptionalFields ? input.ScreeningDecisionId : null,
            includeOptionalFields ? input.WorkId : null,
            includeOptionalFields ? input.DedupClusterId : null,
            includeOptionalFields ? acquisition.SourceReference : null,
            new Dictionary<string, string> { ["format"] = "utf-8", ["origin"] = "local" },
            includeOptionalFields ? "fulltext/source.txt" : null,
            includeOptionalFields ? "source.txt" : null,
            ["warning-a", "warning-b"], [], ["raw-bytes-not-redistribution-authority"]);
        return (input, acquisition, artifact, rawBytes);
    }

    private static ContentDigest Digest(byte[] bytes) => ContentDigest.Sha256(bytes);

    private static byte[] Mutate(byte[] bytes, Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(bytes)!.AsObject();
        mutation(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
    }

    private static void AssertInvalid(Action action, string category = FullTextErrorCodes.InvalidAuthorityChain)
    {
        var error = Assert.ThrowsExactly<FullTextRuleException>(action);
        Assert.AreEqual(category, error.Category);
    }
}
