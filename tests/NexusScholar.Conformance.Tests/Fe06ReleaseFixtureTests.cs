using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AppServices;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Workflow;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class Fe06ReleaseFixtureTests
{
    private const string SourceKind = "local-fe06-release-contract";
    private const string SourceCommit = "2dd5859";
    private const string GeneratorVersion = "fe06-release-local-v1";
    private static readonly string[] FixtureIds =
    [
        "bundle-v2-contract-v1",
        "export-ledger-contract-v1",
        "rapid-review-contract-v1"
    ];

    [TestMethod]
    public void Fe06_release_catalog_is_complete_and_explicitly_local()
    {
        using var catalog = Load("catalog.json");
        var root = catalog.RootElement;
        Assert.AreEqual("nexus.fe06.release.fixture-catalog.v1", Text(root, "schema"));
        Assert.AreEqual(CanonicalJsonSerializer.ProfileId, Text(root, "canonicalizationProfile"));
        CollectionAssert.AreEqual(FixtureIds, TextArray(root, "fixtures"));
        AssertMetadata(root);

        foreach (var id in FixtureIds)
        {
            using var fixture = Load($"{id}.json");
            Assert.AreEqual(id, Text(fixture.RootElement, "fixtureId"));
            AssertMetadata(fixture.RootElement);
        }
    }

    [TestMethod]
    public void Rapid_review_fixture_pins_protected_authority_contracts()
    {
        using var fixture = Load("rapid-review-contract-v1.json");
        var expected = fixture.RootElement.GetProperty("expected");
        Assert.AreEqual(RapidReviewProfileConstants.SchemaId, Text(expected, "profileSchemaId"));
        Assert.AreEqual(RapidReviewProfileConstants.SchemaVersion, Text(expected, "profileSchemaVersion"));
        Assert.AreEqual(ProtocolDeviationConstants.SchemaId, Text(expected, "deviationSchemaId"));
        Assert.AreEqual(ProtocolDeviationConstants.SchemaVersion, Text(expected, "deviationSchemaVersion"));
        Assert.IsFalse(expected.GetProperty("scientificConductDefaults").GetBoolean());
        Assert.AreEqual(ReviewExportActorKinds.Human, Text(expected, "actorKind"));
        CollectionAssert.IsSubsetOf(new[]
        {
            "automation-actor-rejected", "missing-human-approval-rejected", "missing-invalidation-policy-rejected",
            "missing-mitigation-artifact-rejected", "unresolved-deviation-blocks-reporting"
        }, TextArray(expected, "negativeCases"));
        CollectionAssert.AreEqual(RapidReviewProfileConstants.ProtectedInvariants.ToArray(),
            TextArray(expected, "protectedInvariants"));
    }

    [TestMethod]
    public void Bundle_v2_fixture_replays_exact_and_external_inventory_rules()
    {
        using var fixture = Load("bundle-v2-contract-v1.json");
        var expected = fixture.RootElement.GetProperty("expected");
        Assert.AreEqual(BundleV2Constants.SchemaId, Text(expected, "schemaId"));
        Assert.AreEqual(BundleV2Constants.SchemaVersion, Text(expected, "schemaVersion"));
        Assert.AreEqual(BundleV2Constants.ManifestPath, Text(expected, "manifestPath"));
        Assert.IsTrue(expected.GetProperty("canonicalReportRequired").GetBoolean());
        Assert.IsTrue(expected.GetProperty("exactInventory").GetBoolean());
        CollectionAssert.IsSubsetOf(new[]
        {
            "altered-artifact-rejected", "duplicate-path-rejected", "extra-artifact-rejected",
            "foreign-generation-rejected", "missing-artifact-rejected", "mis-scoped-digest-rejected", "traversal-rejected"
        }, TextArray(expected, "negativeCases"));

        var report = System.Text.Encoding.UTF8.GetBytes("fixture report");
        var source = new BundleV2SourceBinding("reporting", "generation-1",
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("source")));
        var manifest = ReviewBundleV2Authority.Create("fixture-bundle",
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("report")),
            "workspace-1", 1,
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("cut")), [source],
            [
                new BundleV2EmbeddedEntry(1, "report.json", report.Length,
                    new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256(report)),
                    "canonical-report", source),
                new BundleV2ExternalEntry("external-1", "doi", "doi:10.1000/example", "Publisher-held bytes.",
                    null, "source-publication", source)
            ], ["External bytes were not verified."]);
        var bytes = ReviewBundleV2CanonicalCodec.Serialize(manifest);
        var verification = ReviewBundleV2Verifier.Verify(manifest, bytes,
            [new BundleV2ObservedEntry("manifest.json", bytes), new BundleV2ObservedEntry("report.json", report)]);
        Assert.IsTrue(verification.IsValid);
        Assert.AreEqual(expected.GetProperty("externalMakesSelfContained").GetBoolean(), verification.IsSelfContained);
    }

    [TestMethod]
    public void Export_fixture_pins_pointer_last_nonmutating_replay_contract()
    {
        using var fixture = Load("export-ledger-contract-v1.json");
        var expected = fixture.RootElement.GetProperty("expected");
        Assert.AreEqual(WorkspaceExportSchemas.LedgerEntryId, Text(expected, "entrySchemaId"));
        Assert.AreEqual(WorkspaceExportSchemas.Version, Text(expected, "entrySchemaVersion"));
        Assert.AreEqual(WorkspaceExportSchemas.LedgerHeadId, Text(expected, "headSchemaId"));
        Assert.AreEqual(WorkspaceExportSchemas.Version, Text(expected, "headSchemaVersion"));
        Assert.AreEqual(ReviewExportActorKinds.Human, Text(expected, "actorKind"));
        Assert.IsTrue(expected.GetProperty("pointerLast").GetBoolean());
        Assert.IsFalse(expected.GetProperty("projectRevisionChanges").GetBoolean());
        Assert.IsTrue(expected.GetProperty("inventoryRecomputed").GetBoolean());
        CollectionAssert.IsSubsetOf(new[]
        {
            "false-inventory-digest-rejected", "manifest-ledger-mismatch-rejected", "process-crash-orphan-quarantined",
            "report-slice-mismatch-rejected", "source-generation-drift-rejected", "stale-head-rejected", "unmanifested-file-rejected"
        }, TextArray(expected, "negativeCases"));
    }

    private static void AssertMetadata(JsonElement root)
    {
        Assert.AreEqual(SourceKind, Text(root, "sourceKind"));
        Assert.AreEqual(SourceCommit, Text(root, "sourceCommit"));
        Assert.AreEqual(GeneratorVersion, Text(root, "generatorVersion"));
        CollectionAssert.IsSubsetOf(new[] { "no-php-compatibility-claim", "no-prisma-certification-claim" },
            TextArray(root, "nonClaims"));
        CollectionAssert.IsSubsetOf(new[]
        {
            "no-blueprint-conformance-claim", "no-php-compatibility-claim", "no-prisma-certification-claim"
        }, TextArray(root, "comparisonRules"));
        CollectionAssert.Contains(TextArray(root, "sourceRefs"),
            "docs/adr/0033-reporting-audit-bundle-and-rapid-review-profile.md");
    }

    private static string Text(JsonElement root, string name) => root.GetProperty(name).GetString()!;
    private static string[] TextArray(JsonElement root, string name) => root.GetProperty(name).EnumerateArray()
        .Select(item => item.GetString()!).OrderBy(item => item, StringComparer.Ordinal).ToArray();
    private static JsonDocument Load(string name) => JsonDocument.Parse(
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "fe06-release", name)));
}
