using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Reporting;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class ReviewArtifactVerificationCommandTests
{
    [TestMethod]
    public void Report_bundle_and_export_verification_reopen_persisted_bytes()
    {
        using var workspace = ExportFixture.Create();

        Assert.AreEqual(0, Run(workspace.Root, ["report", "verify", "export-1"], out var report, out var reportError), reportError);
        StringAssert.Contains(report, "Authority replay: not performed");
        Assert.AreEqual(0, Run(workspace.Root, ["bundle", "verify", "export-1"], out var bundle, out var bundleError), bundleError);
        StringAssert.Contains(bundle, "Verification: valid exact Bundle v2 inventory");
        Assert.AreEqual(0, Run(workspace.Root, ["export", "verify", "export-1"], out var export, out var exportError), exportError);
        StringAssert.Contains(export, "Recorded by: reviewer-1 (human)");
    }

    [TestMethod]
    public void Export_status_replays_complete_history_and_empty_workspace()
    {
        using var workspace = ExportFixture.Create();
        Assert.AreEqual(0, Run(workspace.Root, ["export", "status"], out var output, out var error), error);
        StringAssert.Contains(output, "Count: 1");
        StringAssert.Contains(output, "1: export-1");

        using var empty = ExportFixture.CreateEmpty();
        Assert.AreEqual(0, Run(empty.Root, ["export", "status"], out var emptyOutput, out var emptyError), emptyError);
        StringAssert.Contains(emptyOutput, "Count: 0");
        StringAssert.Contains(emptyOutput, "Head: none");
    }

    [TestMethod]
    public void Verification_rejects_tampered_export_and_missing_identity()
    {
        using var workspace = ExportFixture.Create();
        File.AppendAllText(Path.Combine(workspace.ExportRoot, "bundle", "reports", "report.json"), " ");

        Assert.AreEqual(ResearchWorkspaceExitCodes.DigestMismatch,
            Run(workspace.Root, ["bundle", "verify", "export-1"], out _, out var tamperError));
        StringAssert.Contains(tamperError, "verification failed");

        using var valid = ExportFixture.Create();
        Assert.AreEqual(ResearchWorkspaceExitCodes.MissingProjectOrInput,
            Run(valid.Root, ["export", "verify", "missing"], out _, out var missingError));
        StringAssert.Contains(missingError, "not present in the ledger");
    }

    [TestMethod]
    public void Verification_commands_enforce_exact_usage_and_are_advertised()
    {
        using var workspace = ExportFixture.CreateEmpty();
        Assert.AreEqual(ResearchWorkspaceExitCodes.UsageOrValidationFailure,
            Run(workspace.Root, ["report", "verify"], out _, out var error));
        StringAssert.Contains(error, "Usage: nexus report verify <export-id>");
        StringAssert.Contains(CliApplication.Usage, "report verify");
        StringAssert.Contains(CliApplication.Usage, "bundle verify");
        StringAssert.Contains(CliApplication.Usage, "export verify");
        StringAssert.Contains(CliApplication.Usage, "export status");
    }

    private static int Run(string workingDirectory, string[] args, out string output, out string error)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = CliApplication.Run(args, stdout, stderr, workingDirectory,
            () => new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero));
        output = stdout.ToString();
        error = stderr.ToString();
        return code;
    }

    private sealed class ExportFixture : IDisposable
    {
        private ExportFixture(string root, string exportRoot)
        {
            Root = root;
            ExportRoot = exportRoot;
        }

        public string Root { get; }
        public string ExportRoot { get; }

        public static ExportFixture CreateEmpty()
        {
            var root = Path.Combine(Path.GetTempPath(), "nexus-cli-export-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var location = new ResearchWorkspaceLocation(root, Path.Combine(root, ResearchWorkspacePaths.ProjectFileName));
            ResearchWorkspaceStore.WriteProject(location, ResearchWorkspaceProject.Create("CLI export", Now, "workspace-cli-export"));
            return new ExportFixture(root, ResearchWorkspacePaths.InProject(root, ResearchWorkspacePaths.ExportRoot("export-1")));
        }

        public static ExportFixture Create()
        {
            var fixture = CreateEmpty();
            var authorityDigest = ContentDigest.Sha256Utf8("authority").ToString();
            var empty = CanonicalJsonValue.Array();
            var sliceContent = new CanonicalJsonObject().Add("protocol_version_id", "protocol-v1")
                .Add("protocol_content_digest", authorityDigest).Add("workflow_id", "workflow-1").Add("workflow_digest", authorityDigest)
                .Add("deduplication_result_id", "dedup-1").Add("deduplication_result_digest", authorityDigest)
                .Add("snapshot_id", "snapshot-1").Add("snapshot_record_digest", authorityDigest)
                .Add("screening_binding_digest", authorityDigest).Add("screening_policy_digest", authorityDigest)
                .Add("screening_handoff_digest", authorityDigest).Add("full_text_cases", empty)
                .Add("waiver_digests", empty).Add("amendment_digests", empty).Add("deviation_digests", empty)
                .Add("provenance_event_digests", empty).Add("workspace_id", "workspace-cli-export").Add("project_revision", 0)
                .Add("workspace_generations", CanonicalJsonValue.Array(new CanonicalJsonObject().Add("role", "reporting")
                    .Add("generation_id", "generation-1").Add("manifest_digest", authorityDigest)))
                .Add("workspace_cut_digest", authorityDigest);
            var slice = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReportingSchemas.SliceBindingId, ReportingSchemas.Version, sliceContent);
            var sliceBytes = slice.ToCanonicalJsonBytes();
            var bindings = new CanonicalJsonObject().Add("slice_digest", slice.ComputeDigest().ToString())
                .Add("protocol_content_digest", authorityDigest).Add("workflow_digest", authorityDigest)
                .Add("deduplication_result_digest", authorityDigest).Add("snapshot_record_digest", authorityDigest)
                .Add("screening_binding_digest", authorityDigest).Add("screening_handoff_digest", authorityDigest)
                .Add("full_text_cases", empty).Add("waiver_digests", empty).Add("amendment_digests", empty)
                .Add("deviation_digests", empty).Add("provenance_event_digests", empty).Add("workspace_cut_digest", authorityDigest);
            var counts = new CanonicalJsonObject().Add("identified", 0).Add("duplicates_consolidated", 0).Add("post_dedup", 0)
                .Add("title_abstract_included", 0).Add("title_abstract_excluded", 0).Add("full_text_included", 0)
                .Add("full_text_excluded", 0).Add("included", 0);
            var audit = new CanonicalJsonObject().Add("conflicts", 0).Add("adjudications", 0).Add("corrections", 0).Add("invalidations", 0);
            var report = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReportingSchemas.ReportId, ReportingSchemas.Version,
                new CanonicalJsonObject().Add("bindings", bindings).Add("counts", counts)
                    .Add("title_abstract_exclusion_reasons", empty).Add("full_text_exclusion_reasons", empty)
                    .Add("audit_counts", audit).Add("disclosures", empty)
                    .Add("non_claims", CanonicalJsonValue.Array(CanonicalJsonValue.From("Fixture-only report."))));
            var reportBytes = report.ToCanonicalJsonBytes();
            var reportDigest = report.ComputeDigest();
            var markdownBytes = System.Text.Encoding.UTF8.GetBytes("# Fixture report\n");
            var sourceDigest = ContentDigest.Sha256Utf8("source-manifest");
            var source = new BundleV2SourceBinding("reporting", "generation-1",
                new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), sourceDigest));
            var workspaceCutDigest = ContentDigest.Sha256Utf8("workspace-cut");
            var bundle = ReviewBundleV2Authority.Create("bundle-export-1",
                new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), reportDigest), "workspace-cli-export", 0,
                new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), workspaceCutDigest), [source],
                [new BundleV2EmbeddedEntry(1, "reports/report.json", reportBytes.Length,
                    new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256(reportBytes)),
                    "canonical-report", source)], ["Fixture-only export."]);
            var manifestBytes = ReviewBundleV2CanonicalCodec.Serialize(bundle);
            var observed = new[]
            {
                new BundleV2ObservedEntry("manifest.json", manifestBytes),
                new BundleV2ObservedEntry("reports/report.json", reportBytes)
            };
            var verification = ReviewBundleV2Verifier.Verify(bundle, manifestBytes, observed);
            var sources = CanonicalJsonValue.Array(new CanonicalJsonObject().Add("role", source.Role)
                .Add("generation_id", source.GenerationId).Add("manifest_digest", sourceDigest.ToString()));
            var artifacts = CanonicalJsonValue.Array(observed.OrderBy(item => item.Path, StringComparer.Ordinal).Select(item =>
                new CanonicalJsonObject().Add("path", item.Path).Add("size_bytes", item.Bytes.LongLength)
                    .Add("digest", ContentDigest.Sha256(item.Bytes).ToString())).ToArray());
            var request = new CanonicalJsonObject().Add("export_id", "export-1").Add("actor_id", "reviewer-1")
                .Add("actor_kind", "human").Add("recorded_at", "2026-07-16T08:00:00Z").Add("workspace_id", "workspace-cli-export")
                .Add("project_revision", 0).Add("report_digest", reportDigest.ToString())
                .Add("workspace_cut_digest", workspaceCutDigest.ToString()).Add("bundle_manifest_digest", verification.ManifestDigest.ToString())
                .Add("observed_inventory_digest", verification.InventoryDigest.ToString()).Add("slice_digest", slice.ComputeDigest().ToString())
                .Add("report_markdown_digest", ContentDigest.Sha256(markdownBytes).ToString()).Add("artifacts", artifacts)
                .Add("source_generations", sources);
            var requestBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(request);
            var entryContent = new CanonicalJsonObject().Add("ordinal", 1)
                .Add("previous_entry_digest", WorkspaceExportSchemas.GenesisPreviousDigest).Add("export_id", "export-1")
                .Add("request_digest", ContentDigest.Sha256(requestBytes).ToString()).Add("actor_id", "reviewer-1")
                .Add("actor_kind", "human").Add("recorded_at", "2026-07-16T08:00:00Z")
                .Add("workspace_id", "workspace-cli-export").Add("project_revision", 0)
                .Add("workspace_cut_digest", workspaceCutDigest.ToString()).Add("source_generations", sources)
                .Add("report_digest", reportDigest.ToString()).Add("bundle_manifest_digest", verification.ManifestDigest.ToString())
                .Add("observed_inventory_digest", verification.InventoryDigest.ToString());
            var entry = new DigestEnvelope(DigestScope.CanonicalJsonRecord, WorkspaceExportSchemas.LedgerEntryId,
                WorkspaceExportSchemas.Version, entryContent);
            var entryBytes = entry.ToCanonicalJsonBytes();
            var entryDigest = entry.ComputeDigest();

            Directory.CreateDirectory(Path.Combine(fixture.ExportRoot, "bundle", "reports"));
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, WorkspaceExportSchemas.RequestFileName), requestBytes);
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, "review-slice.json"), sliceBytes);
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, "report.json"), reportBytes);
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, "report.md"), markdownBytes);
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, "bundle", "manifest.json"), manifestBytes);
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, "bundle", "reports", "report.json"), reportBytes);
            File.WriteAllBytes(Path.Combine(fixture.ExportRoot, WorkspaceExportSchemas.EntryFileName), entryBytes);
            var head = new CanonicalJsonObject().Add("schema", WorkspaceExportSchemas.LedgerHeadId)
                .Add("schema_version", WorkspaceExportSchemas.Version).Add("workspace_id", "workspace-cli-export")
                .Add("count", 1).Add("export_id", "export-1")
                .Add("entry_path", $"{ResearchWorkspacePaths.ExportRoot("export-1")}/{WorkspaceExportSchemas.EntryFileName}")
                .Add("entry_digest", entryDigest.ToString());
            File.WriteAllBytes(ResearchWorkspacePaths.InProject(fixture.Root, ResearchWorkspacePaths.ExportLedgerHead),
                CanonicalJsonSerializer.SerializeToUtf8Bytes(head));
            return fixture;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }

        private static readonly DateTimeOffset Now = new(2026, 7, 16, 8, 0, 0, TimeSpan.Zero);
    }
}
