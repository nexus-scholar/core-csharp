using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Bundles;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class BundleV2Tests
{
    [TestMethod]
    public void Bundle_v2_round_trips_exact_embedded_inventory_and_explicit_external_reference()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("verified report");
        var source = Source();
        var manifest = ReviewBundleV2Authority.Create("bundle-2", ReportDigest(), "workspace-1", 7, WorkspaceCutDigest(),
            [source],
            [
                new BundleV2EmbeddedEntry(1, "reports/report.json", payload.Length,
                    new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256(payload)), "canonical-report", source),
                new BundleV2ExternalEntry("external-1", "doi", "doi:10.1000/example", "Available from the publisher.",
                    new BundleV2ScopedDigest("canonical-json-record", ContentDigest.Sha256Utf8("expected")), "source-publication", source)
            ], ["External bytes were not verified."]);
        var bytes = ReviewBundleV2CanonicalCodec.Serialize(manifest);
        var inventory = new[] { new BundleV2ObservedEntry(BundleV2Constants.ManifestPath, bytes), new BundleV2ObservedEntry("reports/report.json", payload) };
        var verified = ReviewBundleV2Verifier.Verify(manifest, bytes, inventory);

        Assert.IsTrue(verified.IsValid);
        Assert.IsFalse(verified.IsSelfContained);
        Assert.AreEqual(manifest.ManifestDigest, ReviewBundleV2CanonicalCodec.Rehydrate(bytes, manifest.ManifestDigest).ManifestDigest);
    }

    [TestMethod]
    public void Bundle_v2_detects_missing_extra_and_altered_inventory()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("payload");
        var manifest = EmbeddedManifest(payload);
        var manifestBytes = ReviewBundleV2CanonicalCodec.Serialize(manifest);
        var missing = ReviewBundleV2Verifier.Verify(manifest, manifestBytes,
            [new BundleV2ObservedEntry(BundleV2Constants.ManifestPath, manifestBytes)]);
        var extra = ReviewBundleV2Verifier.Verify(manifest, manifestBytes,
            [new BundleV2ObservedEntry(BundleV2Constants.ManifestPath, manifestBytes), new BundleV2ObservedEntry("data/item.txt", payload), new BundleV2ObservedEntry("extra.txt", [])]);
        var altered = ReviewBundleV2Verifier.Verify(manifest, manifestBytes,
            [new BundleV2ObservedEntry(BundleV2Constants.ManifestPath, manifestBytes), new BundleV2ObservedEntry("data/item.txt", System.Text.Encoding.UTF8.GetBytes("altered"))]);

        Assert.IsTrue(missing.Findings.Any(item => item.Category == BundleV2ErrorCodes.MissingInventory));
        Assert.IsTrue(extra.Findings.Any(item => item.Category == BundleV2ErrorCodes.ExtraInventory));
        Assert.IsTrue(altered.Findings.Any(item => item.Category == BundleV2ErrorCodes.AlteredArtifact));
    }

    [TestMethod]
    public void Bundle_v2_rejects_traversal_misscoped_duplicate_and_foreign_entries()
    {
        var source = Source();
        var digest = new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256Utf8("x"));
        Assert.AreEqual(BundleV2ErrorCodes.InvalidPath, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-path", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2EmbeddedEntry(1, "../escape", 1, digest, "report", source)], ["No compatibility claim."])).Category);
        Assert.AreEqual(BundleV2ErrorCodes.MisScopedDigest, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-scope", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2EmbeddedEntry(1, "item", 1, digest with { Scope = "canonical-json-record" }, "report", source)], ["No compatibility claim."])).Category);
        Assert.AreEqual(BundleV2ErrorCodes.DuplicateEntry, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-duplicate", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2EmbeddedEntry(1, "item", 1, digest, "report", source), new BundleV2EmbeddedEntry(2, "item", 1, digest, "report", source)],
            ["No compatibility claim."])).Category);
        Assert.AreEqual(BundleV2ErrorCodes.ForeignGeneration, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-foreign", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2EmbeddedEntry(1, "item", 1, digest, "report", source with { GenerationId = "other" })], ["No compatibility claim."])).Category);
    }

    [TestMethod]
    public void External_reference_cannot_hide_bytes_paths_or_credentials()
    {
        var source = Source();
        Assert.AreEqual(BundleV2ErrorCodes.InvalidExternalReference, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-external", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2ExternalEntry("external", "repository", "https://user:secret@example.test/item", "Available.", null, "source", source)],
            ["No compatibility claim."])).Category);
        Assert.AreEqual(BundleV2ErrorCodes.InvalidExternalReference, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-file", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2ExternalEntry("external", "repository", "file:///c:/private/item", "Available.", null, "source", source)],
            ["No compatibility claim."])).Category);
        Assert.AreEqual(BundleV2ErrorCodes.InvalidExternalReference, Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2Authority.Create(
            "bundle-query", ReportDigest(), "workspace", 1, WorkspaceCutDigest(), [source],
            [new BundleV2ExternalEntry("external", "repository", "https://example.test/item?sig=secret", "Available.", null, "source", source)],
            ["No compatibility claim."])).Category);
    }

    [TestMethod]
    public void Verifier_detects_duplicate_observed_paths_and_wrong_manifest_inventory_bytes()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("payload");
        var manifest = EmbeddedManifest(payload);
        var bytes = ReviewBundleV2CanonicalCodec.Serialize(manifest);
        var result = ReviewBundleV2Verifier.Verify(manifest, bytes,
        [
            new BundleV2ObservedEntry(BundleV2Constants.ManifestPath, System.Text.Encoding.UTF8.GetBytes("wrong")),
            new BundleV2ObservedEntry("data/item.txt", payload),
            new BundleV2ObservedEntry("data/item.txt", payload)
        ]);

        Assert.IsTrue(result.Findings.Any(item => item.Category == BundleV2ErrorCodes.DuplicateEntry));
        Assert.IsTrue(result.Findings.Any(item => item.Category == BundleV2ErrorCodes.AlteredArtifact));
    }

    [TestMethod]
    public void Strict_codec_rejects_unknown_fields_and_noncanonical_bytes()
    {
        var manifest = EmbeddedManifest(System.Text.Encoding.UTF8.GetBytes("payload"));
        var bytes = ReviewBundleV2CanonicalCodec.Serialize(manifest);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var altered = System.Text.Encoding.UTF8.GetBytes(text.Replace("\"workspace_id\":", "\"unknown\":true,\"workspace_id\":", StringComparison.Ordinal));
        var duplicate = System.Text.Encoding.UTF8.GetBytes(text.Replace("\"bundle_id\":", "\"bundle_id\":\"duplicate\",\"bundle_id\":", StringComparison.Ordinal));

        Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2CanonicalCodec.Rehydrate(altered, ContentDigest.Sha256(altered)));
        Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2CanonicalCodec.Rehydrate(duplicate, ContentDigest.Sha256(duplicate)));
        Assert.ThrowsExactly<BundleV2Exception>(() => ReviewBundleV2CanonicalCodec.Rehydrate(bytes.Concat([(byte)' ']).ToArray(), manifest.ManifestDigest));
    }

    private static VerifiedReviewBundleV2 EmbeddedManifest(byte[] payload)
    {
        var source = Source();
        return ReviewBundleV2Authority.Create("bundle-embedded", ReportDigest(), "workspace-1", 7, WorkspaceCutDigest(), [source],
            [new BundleV2EmbeddedEntry(1, "data/item.txt", payload.Length,
                new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256(payload)), "canonical-report", source)],
            ["No archive-format identity claim."]);
    }

    private static BundleV2SourceBinding Source() => new("reporting", "generation-1",
        new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("manifest")));
    private static BundleV2ScopedDigest ReportDigest() => new(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("report"));
    private static BundleV2ScopedDigest WorkspaceCutDigest() => new(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("workspace-cut"));
}
