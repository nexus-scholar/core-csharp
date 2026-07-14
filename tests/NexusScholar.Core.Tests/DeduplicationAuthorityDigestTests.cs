using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class DeduplicationAuthorityDigestTests
{
    [TestMethod]
    public void Create_candidate_digest_material_is_deterministic_when_set_like_collections_reorder()
    {
        var left = BuildCandidate(
            "candidate-a",
            workIds: new[] { "w-b", "w-a" },
            sourceSpecificIds: new[] { "s-2", "s-1" });
        var right = BuildCandidate(
            "candidate-a",
            workIds: new[] { "w-a", "w-b" },
            sourceSpecificIds: new[] { "s-1", "s-2" });

        var leftDigest = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(left).CandidateDigest;
        var rightDigest = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(right).CandidateDigest;

        Assert.AreEqual(leftDigest, rightDigest);
    }

    [TestMethod]
    public void Candidate_digest_treats_author_order_as_semantic()
    {
        var ordered = BuildCandidate("candidate-a", authors: new[] { "Alice", "Bob" });
        var reversed = BuildCandidate("candidate-a", authors: new[] { "Bob", "Alice" });

        var orderedDigest = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(ordered).CandidateDigest;
        var reversedDigest = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(reversed).CandidateDigest;

        Assert.AreNotEqual(orderedDigest, reversedDigest);
    }

    [TestMethod]
    public void Candidate_digest_normalizes_keyword_order_but_preserves_verified_collection_ownership()
    {
        var mutableWorkIds = new List<string> { "w-b", "w-a" };
        var left = BuildCandidate("candidate-a", workIds: mutableWorkIds, keywords: new[] { "zeta", "alpha" });
        var right = BuildCandidate("candidate-a", workIds: new[] { "w-a", "w-b" }, keywords: new[] { "alpha", "zeta" });

        var verified = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(left);
        mutableWorkIds.Add("caller-mutation");

        Assert.AreEqual(
            DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(right).CandidateDigest,
            verified.CandidateDigest);
        CollectionAssert.DoesNotContain(verified.Candidate.WorkIds.ToArray(), "caller-mutation");
    }

    [TestMethod]
    public void Create_result_digest_material_normalizes_set_like_collections_for_determinism()
    {
        var first = BuildCandidate("candidate-1");
        var second = BuildCandidate("candidate-2");
        var evidenceFirst = BuildEvidence("evidence-1", first.CandidateId, second.CandidateId);
        var evidenceSecond = BuildEvidence("evidence-2", second.CandidateId, first.CandidateId);
        var resultOne = BuildResult(
            new[] { second, first },
            new[] { evidenceSecond, evidenceFirst },
            new[] { "trace-b", "trace-a" },
            new[] { new DedupReviewCandidate(second.CandidateId, first.CandidateId, 0.93, 0.88) });

        var resultTwo = BuildResult(
            new[] { first, second },
            new[] { evidenceFirst, evidenceSecond },
            new[] { "trace-a", "trace-b" },
            new[] { new DedupReviewCandidate(first.CandidateId, second.CandidateId, 0.93, 0.88) });

        var digestOne = DeduplicationAuthorityDigests.CreateResultDigestMaterial(resultOne).ResultDigest;
        var digestTwo = DeduplicationAuthorityDigests.CreateResultDigestMaterial(resultTwo).ResultDigest;

        Assert.AreEqual(digestOne, digestTwo);
    }

    [TestMethod]
    public void Rehydrate_candidate_digest_material_rejects_non_canonical_input()
    {
        var canonical = BuildCandidate(
            "candidate-1",
            workIds: new[] { "w-a", "w-b" },
            sourceSpecificIds: new[] { "s-1", "s-2" });
        var material = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(canonical);

        var persisted = canonical with
        {
            WorkIds = Array.AsReadOnly(new[] { "w-b", "w-a" }),
            SourceSpecificIds = Array.AsReadOnly(new[] { "s-2", "s-1" })
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateCandidateDigestMaterial(new(persisted, material.CandidateDigest)));
        Assert.AreEqual(DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial, error.Category);
    }

    [TestMethod]
    public void Rehydrate_candidate_digest_material_rejects_non_nfc_and_malformed_digest_material()
    {
        var canonical = BuildCandidate("candidate-1");
        var material = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(canonical);

        var nonNfc = canonical with { Title = "Cafe\u0301" };
        var nfcError = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateCandidateDigestMaterial(new(nonNfc, material.CandidateDigest)));
        Assert.AreEqual(DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial, nfcError.Category);

        var malformed = canonical with { Source = canonical.Source with { SourceFileDigest = "SHA256:NOT-CANONICAL" } };
        var digestError = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateCandidateDigestMaterial(new(malformed, material.CandidateDigest)));
        Assert.AreEqual(DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial, digestError.Category);
    }

    [TestMethod]
    public void Rehydrate_evidence_digest_material_rejects_tampered_material()
    {
        var evidence = BuildEvidence("evidence-1", "candidate-1", "candidate-2");
        var material = DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(evidence);
        var tampered = evidence with { Score = 0.11 };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateEvidenceDigestMaterial(new(tampered, material.EvidenceDigest)));
        Assert.AreEqual(DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding, error.Category);
    }

    [TestMethod]
    public void Create_review_target_digest_material_normalizes_pair_and_target_candidate_ids()
    {
        var first = BuildCandidate("candidate-1");
        var second = BuildCandidate("candidate-2");
        var evidence = BuildEvidence("evidence-1", "candidate-2", "candidate-1");
        var result = BuildResult(new[] { second, first }, new[] { evidence }, new[] { "trace-a" }, new[] { new DedupReviewCandidate("candidate-2", "candidate-1", 0.9, 0.8) });
        var verifiedResult = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);

        var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(
            verifiedResult,
            new DedupReviewCandidate("candidate-2", "candidate-1", 0.9, 0.8),
            new[] { "candidate-2", "candidate-1" },
            new[] { evidence });

        Assert.AreEqual("candidate-1", target.ReviewPair.CandidateAId);
        Assert.AreEqual("candidate-2", target.ReviewPair.CandidateBId);
        CollectionAssert.AreEqual(new[] { "candidate-1", "candidate-2" }, target.CandidateIds.ToArray());
    }

    [TestMethod]
    public void Create_review_target_digest_material_rejects_duplicate_evidence()
    {
        var first = BuildCandidate("candidate-1");
        var second = BuildCandidate("candidate-2");
        var evidence = BuildEvidence("evidence-1", "candidate-1", "candidate-2");
        var result = BuildResult(new[] { first, second }, new[] { evidence }, new[] { "trace-a" }, new[] { new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8) });
        var verifiedResult = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);

        var duplicates = new[] { evidence, evidence };
        Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(
                verifiedResult,
                new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8),
                new[] { "candidate-1", "candidate-2" },
                duplicates));
    }

    [TestMethod]
    public void Rehydrate_review_target_digest_material_rejects_non_canonical_input()
    {
        var first = BuildCandidate("candidate-1");
        var second = BuildCandidate("candidate-2");
        var evidence = BuildEvidence("evidence-1", "candidate-1", "candidate-2");
        var result = BuildResult(new[] { first, second }, new[] { evidence }, new[] { "trace-a" }, new[] { new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8) });
        var verifiedResult = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
        var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(
            verifiedResult,
            new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8),
            new[] { "candidate-1", "candidate-2" },
            new[] { evidence });

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateReviewTargetDigestMaterial(
                verifiedResult,
                new(
                    SchemaId: DeduplicationAuthorityDigests.ReviewTargetSchemaId,
                    SchemaVersion: DeduplicationAuthorityDigests.ReviewTargetSchemaVersion,
                    TargetKind: target.TargetKind,
                    TargetId: target.TargetId,
                    SourceResultId: verifiedResult.Result.ResultId,
                    SourceResultDigest: verifiedResult.ResultDigest,
                    CandidateIds: new[] { "candidate-2", "candidate-1" },
                    ReviewPair: new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8),
                    Evidence: new[] { evidence },
                    TargetDigest: target.TargetDigest)));
        Assert.AreEqual(DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial, error.Category);
    }

    [TestMethod]
    public void Rehydrate_review_target_digest_material_rejects_mismatched_evidence_binding()
    {
        var first = BuildCandidate("candidate-1");
        var second = BuildCandidate("candidate-2");
        var third = BuildCandidate("candidate-3");
        var pairEvidence = BuildEvidence("evidence-pair", "candidate-1", "candidate-2");
        var mismatchedEvidence = BuildEvidence("evidence-mismatch", "candidate-1", "candidate-3");
        var result = BuildResult(
            new[] { first, second, third },
            new[] { pairEvidence, mismatchedEvidence },
            new[] { "trace-a" },
            new[] { new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8) });
        var verifiedResult = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
        var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(
            verifiedResult,
            new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8),
            new[] { "candidate-1", "candidate-2" },
            new[] { pairEvidence });

        var mutated = target.TargetDigest;
        var mismatch = new UnverifiedDeduplicationAuthorityReviewTargetDigest(
            DeduplicationAuthorityDigests.ReviewTargetSchemaId,
            DeduplicationAuthorityDigests.ReviewTargetSchemaVersion,
            target.TargetKind,
            target.TargetId,
            verifiedResult.Result.ResultId,
            verifiedResult.ResultDigest,
            new[] { "candidate-1", "candidate-2" },
            new DedupReviewCandidate("candidate-1", "candidate-2", 0.9, 0.8),
            new[] { mismatchedEvidence },
            mutated);

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateReviewTargetDigestMaterial(verifiedResult, mismatch));
        Assert.AreEqual(DeduplicationAuthorityErrorCodes.InvalidEvidence, error.Category);
    }

    [TestMethod]
    public void Rehydrate_review_target_rejects_persisted_descriptor_mismatch()
    {
        var first = BuildCandidate("candidate-1");
        var second = BuildCandidate("candidate-2");
        var evidence = BuildEvidence("evidence-1", first.CandidateId, second.CandidateId);
        var verifiedResult = DeduplicationAuthorityDigests.CreateResultDigestMaterial(BuildResult(
            new[] { first, second },
            new[] { evidence },
            new[] { "trace-a" },
            new[] { new DedupReviewCandidate(first.CandidateId, second.CandidateId, 0.9, 0.8) }));
        var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(
            verifiedResult,
            new DedupReviewCandidate(first.CandidateId, second.CandidateId, 0.9, 0.8),
            new[] { first.CandidateId, second.CandidateId },
            new[] { evidence });

        var persisted = new UnverifiedDeduplicationAuthorityReviewTargetDigest(
            DeduplicationAuthorityDigests.ReviewTargetSchemaId,
            DeduplicationAuthorityDigests.ReviewTargetSchemaVersion,
            target.TargetKind,
            "wrong-target-id",
            verifiedResult.Result.ResultId,
            verifiedResult.ResultDigest,
            target.CandidateIds,
            target.ReviewPair,
            target.Evidence,
            target.TargetDigest);

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityDigests.RehydrateReviewTargetDigestMaterial(verifiedResult, persisted));
        Assert.AreEqual(DeduplicationAuthorityDigestErrorCodes.InvalidAuthorityTarget, error.Category);
    }

    private static DeduplicationResult BuildResult(
        IReadOnlyList<DedupCandidateRecord> candidates,
        IReadOnlyList<DedupEvidence> evidence,
        IReadOnlyList<string> sourceSearchTraceIds,
        IReadOnlyList<DedupReviewCandidate> reviewPairs)
    {
        return new DeduplicationResult(
            "result-1",
            DeduplicationService.ResultSchemaId,
            DeduplicationService.ResultSchemaVersion,
            DeduplicationService.PolicyId,
            DeduplicationService.PolicyVersion,
            0.75,
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "search", 2 },
                { "resolver", 1 }
            }),
            sourceSearchTraceIds,
            Array.Empty<string>(),
            candidates,
            Array.Empty<DedupCluster>(),
            evidence,
            Array.Empty<DedupCandidateRecord>(),
            reviewPairs,
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<string>());
    }

    private static DedupCandidateRecord BuildCandidate(
        string id,
        IReadOnlyList<string>? workIds = null,
        IReadOnlyList<string>? sourceSpecificIds = null,
        IReadOnlyList<string>? authors = null,
        IReadOnlyList<string>? keywords = null)
    {
        return new DedupCandidateRecord(
            id,
            $"Title {id}",
            false,
            $"{id}-doi",
            workIds ?? new[] { "id-a", "id-b" },
            sourceSpecificIds ?? new[] { "s-a", "s-b" },
            BuildSighting(id),
            authors ?? new[] { "Author A", "Author B" },
            2026,
            null,
            null,
            keywords ?? new[] { "keyword-a", "keyword-b" });
    }

    private static DedupEvidence BuildEvidence(string evidenceId, string subjectCandidateId, string objectCandidateId)
    {
        return new DedupEvidence(
            evidenceId,
            DedupEvidenceKind.SourceSighting,
            subjectCandidateId,
            objectCandidateId,
            "evidence",
            ReviewRequired: true,
            0.9,
            DeduplicationService.PolicyId,
            DeduplicationService.PolicyVersion);
    }

    private static DedupSightingRef BuildSighting(string id) => new(
        SourceKind: "openalex",
        SourceTraceId: $"trace-{id}",
        SourceSightingId: $"sighting-{id}",
        ProviderAlias: "provider",
        SourceDatabaseOrTool: "tool",
        SourceRecordId: $"record-{id}",
        SourceFileDigest: ContentDigest.Sha256Utf8($"source-{id}").ToString(),
        SourceFileDigestScope: DigestScope.RawArtifactBytes.ToString(),
        RawRecordDigest: ContentDigest.Sha256Utf8($"raw-{id}").ToString(),
        SourceContext: "ctx",
        ParserWarnings: Array.Empty<DedupParserNotice>(),
        RecordNotices: Array.Empty<DedupParserNotice>());
}
