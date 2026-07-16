using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AppServices;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Reporting;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class WorkspaceExportLedgerTests
{
    [TestMethod]
    public void Verified_export_commits_replays_and_leaves_project_bytes_unchanged()
    {
        using var workspace = CreateWorkspace();
        var request = BuildRequest("export-1");
        request.ReportBytes[0] ^= 0xff;
        request.ObservedInventory[0].Bytes[0] ^= 0xff;
        var before = File.ReadAllBytes(workspace.Location.ProjectFilePath);

        var commit = ReviewExportApplicationService.Commit(request, null,
            new ResearchWorkspaceExportCommitPort(workspace.Location, workspace.Project));
        var replay = ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location);
        var retry = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, request, null);

        Assert.AreEqual(1L, commit.Ordinal);
        Assert.AreEqual(commit.EntryDigest, replay.Head!.EntryDigest);
        Assert.IsTrue(retry.AlreadyApplied);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(workspace.Location.ProjectFilePath));
        Assert.AreEqual(4L, ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath).Revision);
    }

    [TestMethod]
    public void Ledger_is_hash_chained_and_stale_predecessor_is_rejected()
    {
        using var workspace = CreateWorkspace();
        var first = ResearchWorkspaceExportTransaction.Commit(
            workspace.Location, workspace.Project, BuildRequest("export-1"), null);
        var second = ResearchWorkspaceExportTransaction.Commit(
            workspace.Location, workspace.Project, BuildRequest("export-2"), first.Entry.Digest);

        Assert.AreEqual(first.Entry.Digest, second.Entry.PreviousEntryDigest);
        var error = Assert.ThrowsExactly<WorkspaceExportException>(() => ResearchWorkspaceExportTransaction.Commit(
            workspace.Location, workspace.Project, BuildRequest("export-3"), first.Entry.Digest));
        Assert.AreEqual(WorkspaceExportErrorCodes.StaleHead, error.Category);
    }

    [TestMethod]
    public void Source_revision_drift_fails_before_promotion()
    {
        using var workspace = CreateWorkspace();
        ResearchWorkspaceStore.WriteProject(workspace.Location, workspace.Project with { Revision = 5 });

        var error = Assert.ThrowsExactly<WorkspaceExportException>(() => ResearchWorkspaceExportTransaction.Commit(
            workspace.Location, workspace.Project, BuildRequest("export-drift"), null));

        Assert.AreEqual(WorkspaceExportErrorCodes.SourceDrift, error.Category);
        Assert.IsFalse(Directory.Exists(ResearchWorkspacePaths.InProject(
            workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportRoot("export-drift"))));
    }

    [TestMethod]
    public void Faults_before_head_publication_leave_no_visible_export()
    {
        foreach (var point in new[]
                 {
                     ResearchWorkspaceExportFaultPoint.AfterStaging,
                     ResearchWorkspaceExportFaultPoint.AfterPromotion,
                     ResearchWorkspaceExportFaultPoint.BeforeHeadPublication
                 })
        {
            using var workspace = CreateWorkspace();
            Assert.ThrowsExactly<InvalidOperationException>(() => ResearchWorkspaceExportTransaction.Commit(
                workspace.Location, workspace.Project, BuildRequest("export-fault"), null,
                faultInjector: observed => { if (observed == point) throw new InvalidOperationException("fault"); }));

            var replay = ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location);
            Assert.IsNull(replay.Head);
            Assert.IsFalse(Directory.Exists(ResearchWorkspacePaths.InProject(
                workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportRoot("export-fault"))));
        }
    }

    [TestMethod]
    public void Fault_after_head_publication_is_recoverable_by_idempotent_retry()
    {
        using var workspace = CreateWorkspace();
        var request = BuildRequest("export-published");
        Assert.ThrowsExactly<InvalidOperationException>(() => ResearchWorkspaceExportTransaction.Commit(
            workspace.Location, workspace.Project, request, null,
            faultInjector: point => { if (point == ResearchWorkspaceExportFaultPoint.AfterHeadPublication) throw new InvalidOperationException("fault"); }));

        var retry = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, request, null);
        Assert.IsTrue(retry.AlreadyApplied);
        Assert.AreEqual(1, ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location).Entries.Count);
    }

    [TestMethod]
    public void Replay_rejects_altered_export_bytes_and_extra_files()
    {
        foreach (var mutation in new[] { "alter", "extra" })
        {
            using var workspace = CreateWorkspace();
            _ = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, BuildRequest("export-tamper"), null);
            var root = ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportRoot("export-tamper"));
            if (mutation == "alter") File.AppendAllText(Path.Combine(root, "report.json"), " ");
            else File.WriteAllText(Path.Combine(root, "undeclared.txt"), "extra");

            var error = Assert.ThrowsExactly<WorkspaceExportException>(() => ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location));
            Assert.AreEqual(WorkspaceExportErrorCodes.InvalidLedger, error.Category);
        }
    }

    [TestMethod]
    public void App_orchestration_rejects_a_bundle_bound_to_another_report()
    {
        var report = BuildReport();
        var reportBytes = ReportingCanonicalCodec.SerializeReport(report);
        var sources = Sources(report);
        var source = sources[0];
        var bundle = ReviewBundleV2Authority.Create("bundle-wrong",
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), ContentDigest.Sha256Utf8("other-report")),
            report.WorkspaceCut.WorkspaceId, report.WorkspaceCut.ProjectRevision,
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), report.WorkspaceCut.Digest), sources,
            [new BundleV2EmbeddedEntry(1, "reports/report.json", reportBytes.Length,
                new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256(reportBytes)),
                "canonical-report", source)], ["No archive identity claim."]);
        var manifestBytes = ReviewBundleV2CanonicalCodec.Serialize(bundle);

        Assert.ThrowsExactly<InvalidOperationException>(() => ReviewExportOrchestrator.Prepare(
            "export-wrong", HumanActor(), "2026-07-16T08:00:00Z", report, bundle, manifestBytes,
            [new BundleV2ObservedEntry("manifest.json", manifestBytes), new BundleV2ObservedEntry("reports/report.json", reportBytes)]));
    }

    [TestMethod]
    public void App_orchestration_rejects_nonhuman_export_actor()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => BuildRequest("export-automation",
            new ReviewExportActor("automation-1", ReviewExportActorKinds.Automation)));
    }

    [TestMethod]
    public void Replay_reports_in_progress_lock_instead_of_observing_promotion_window()
    {
        using var workspace = CreateWorkspace();
        var observedLock = false;
        Assert.ThrowsExactly<InvalidOperationException>(() => ResearchWorkspaceExportTransaction.Commit(
            workspace.Location, workspace.Project, BuildRequest("export-window"), null,
            faultInjector: point =>
            {
                if (point != ResearchWorkspaceExportFaultPoint.AfterPromotion) return;
                Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() =>
                    ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location));
                observedLock = true;
                throw new InvalidOperationException("fault");
            }));
        Assert.IsTrue(observedLock);
    }

    [TestMethod]
    public void Empty_ledger_head_fails_with_stable_category()
    {
        using var workspace = CreateWorkspace();
        var headPath = ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportLedgerHead);
        Directory.CreateDirectory(Path.GetDirectoryName(headPath)!);
        var head = new CanonicalJsonObject().Add("schema", WorkspaceExportSchemas.LedgerHeadId)
            .Add("schema_version", WorkspaceExportSchemas.Version).Add("workspace_id", workspace.Project.WorkspaceId)
            .Add("count", 0).Add("export_id", "none").Add("entry_path", "nexus-output/exports/none/export-ledger-entry.json")
            .Add("entry_digest", ContentDigest.Sha256Utf8("none").ToString());
        File.WriteAllBytes(headPath, CanonicalJsonSerializer.SerializeToUtf8Bytes(head));

        var error = Assert.ThrowsExactly<WorkspaceExportException>(() =>
            ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location));
        Assert.AreEqual(WorkspaceExportErrorCodes.InvalidLedger, error.Category);
    }

    [TestMethod]
    public void Replay_recomputes_inventory_digest_instead_of_trusting_consistent_ledger_text()
    {
        using var workspace = CreateWorkspace();
        _ = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, BuildRequest("export-inventory"), null);
        var exportRoot = ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory,
            ResearchWorkspacePaths.ExportRoot("export-inventory"));
        var wrong = ContentDigest.Sha256Utf8("wrong-inventory").ToString();

        var requestPath = Path.Combine(exportRoot, WorkspaceExportSchemas.RequestFileName);
        var request = JsonNode.Parse(File.ReadAllBytes(requestPath))!.AsObject();
        request["observed_inventory_digest"] = wrong;
        var requestBytes = CanonicalBytes(request);
        File.WriteAllBytes(requestPath, requestBytes);

        var entryPath = Path.Combine(exportRoot, WorkspaceExportSchemas.EntryFileName);
        var entry = JsonNode.Parse(File.ReadAllBytes(entryPath))!.AsObject();
        var content = entry["content"]!.AsObject();
        content["request_digest"] = ContentDigest.Sha256(requestBytes).ToString();
        content["observed_inventory_digest"] = wrong;
        var entryBytes = CanonicalBytes(entry);
        File.WriteAllBytes(entryPath, entryBytes);

        var headPath = ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportLedgerHead);
        var head = JsonNode.Parse(File.ReadAllBytes(headPath))!.AsObject();
        head["entry_digest"] = ContentDigest.Sha256(entryBytes).ToString();
        File.WriteAllBytes(headPath, CanonicalBytes(head));

        var error = Assert.ThrowsExactly<WorkspaceExportException>(() =>
            ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location));
        Assert.AreEqual(WorkspaceExportErrorCodes.InvalidLedger, error.Category);
    }

    [TestMethod]
    public void Retry_recovers_process_crash_orphan_before_republishing()
    {
        using var workspace = CreateWorkspace();
        var request = BuildRequest("export-orphan");
        _ = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, request, null);
        File.Delete(ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportLedgerHead));

        var interrupted = ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location);
        Assert.AreEqual(0, interrupted.Entries.Count);
        CollectionAssert.AreEqual(new[] { "export-orphan" }, interrupted.UnreferencedExportIds.ToArray());

        var recovered = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, request, null);
        Assert.IsFalse(recovered.AlreadyApplied);
        Assert.AreEqual(1, ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location).Entries.Count);
        Assert.IsTrue(Directory.EnumerateDirectories(ResearchWorkspacePaths.InProject(
            workspace.Location.RootDirectory, ResearchWorkspacePaths.GenerationQuarantine), "export-export-orphan-*").Any());
    }

    [TestMethod]
    public void Same_export_bytes_with_another_human_action_are_not_idempotent()
    {
        using var workspace = CreateWorkspace();
        _ = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, BuildRequest("export-human"), null);
        var second = BuildRequest("export-human", new ReviewExportActor("reviewer-2", ReviewExportActorKinds.Human));

        var error = Assert.ThrowsExactly<WorkspaceExportException>(() =>
            ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, second, null));
        Assert.AreEqual(WorkspaceExportErrorCodes.ExportCollision, error.Category);
    }

    [TestMethod]
    public void Replay_rejects_internally_consistent_manifest_or_slice_rebinding()
    {
        using (var workspace = CreateWorkspace())
        {
            _ = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, BuildRequest("export-manifest"), null);
            var root = ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportRoot("export-manifest"));
            var manifestPath = Path.Combine(root, "bundle", "manifest.json");
            var replacement = CanonicalJsonSerializer.SerializeToUtf8Bytes(new CanonicalJsonObject().Add("invalid", "manifest"));
            File.WriteAllBytes(manifestPath, replacement);
            RewriteRequestEntryAndHead(workspace.Location, root, (request, entry) =>
            {
                var artifacts = request["artifacts"]!.AsArray();
                var manifest = artifacts.Select(item => item!.AsObject()).Single(item => item["path"]!.GetValue<string>() == "manifest.json");
                manifest["size_bytes"] = replacement.LongLength;
                manifest["digest"] = ContentDigest.Sha256(replacement).ToString();
                var inventoryDigest = InventoryDigest(artifacts);
                request["observed_inventory_digest"] = inventoryDigest.ToString();
                entry["observed_inventory_digest"] = inventoryDigest.ToString();
            });
            var manifestError = Assert.ThrowsExactly<WorkspaceExportException>(() => ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location));
            Assert.AreEqual(WorkspaceExportErrorCodes.InvalidLedger, manifestError.Category);
        }

        using (var workspace = CreateWorkspace())
        {
            _ = ResearchWorkspaceExportTransaction.Commit(workspace.Location, workspace.Project, BuildRequest("export-slice"), null);
            var root = ResearchWorkspacePaths.InProject(workspace.Location.RootDirectory, ResearchWorkspacePaths.ExportRoot("export-slice"));
            var slicePath = Path.Combine(root, "review-slice.json");
            var slice = JsonNode.Parse(File.ReadAllBytes(slicePath))!.AsObject();
            slice["content"]!["workspace_id"] = "different-workspace";
            var sliceBytes = CanonicalBytes(slice);
            File.WriteAllBytes(slicePath, sliceBytes);
            RewriteRequestEntryAndHead(workspace.Location, root, (request, _) =>
                request["slice_digest"] = ContentDigest.Sha256(sliceBytes).ToString());
            var sliceError = Assert.ThrowsExactly<WorkspaceExportException>(() => ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location));
            Assert.AreEqual(WorkspaceExportErrorCodes.InvalidLedger, sliceError.Category);
        }
    }

    private static VerifiedReviewExportRequest BuildRequest(string exportId, ReviewExportActor? actor = null)
    {
        var report = BuildReport();
        var reportBytes = ReportingCanonicalCodec.SerializeReport(report);
        var sources = Sources(report);
        var source = sources[0];
        var bundle = ReviewBundleV2Authority.Create($"bundle-{exportId}",
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), report.ReportDigest),
            report.WorkspaceCut.WorkspaceId, report.WorkspaceCut.ProjectRevision,
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), report.WorkspaceCut.Digest), sources,
            [new BundleV2EmbeddedEntry(1, "reports/report.json", reportBytes.Length,
                new BundleV2ScopedDigest(DigestScope.RawArtifactBytes.ToString(), ContentDigest.Sha256(reportBytes)),
                "canonical-report", source)], ["No archive identity claim."]);
        var manifestBytes = ReviewBundleV2CanonicalCodec.Serialize(bundle);
        return ReviewExportOrchestrator.Prepare(exportId, actor ?? HumanActor(), "2026-07-16T08:00:00Z", report, bundle,
            manifestBytes, [new BundleV2ObservedEntry("manifest.json", manifestBytes), new BundleV2ObservedEntry("reports/report.json", reportBytes)]);
    }

    private static VerifiedReviewFlowReport BuildReport() => ReviewFlowProjector.Finalize(
        ReviewFlowProjector.Project(ReportingTests.BuildAuthorities(includeFullText: true),
            ["Local-only review."], ["No PRISMA certification claim."]));

    private static ReviewExportActor HumanActor() => new("reviewer-1", ReviewExportActorKinds.Human);

    private static BundleV2SourceBinding[] Sources(VerifiedReviewFlowReport report) => report.WorkspaceCut.Generations
        .Select(item => new BundleV2SourceBinding(item.Role, item.GenerationId,
            new BundleV2ScopedDigest(DigestScope.CanonicalJsonRecord.ToString(), item.ManifestDigest), item.CandidateId)).ToArray();

    private static byte[] CanonicalBytes(JsonNode node)
    {
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(node));
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
    }

    private static ContentDigest InventoryDigest(JsonArray artifacts)
    {
        var paths = artifacts.Select(item => item!.AsObject()).OrderBy(item => item["path"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(item => new CanonicalJsonObject().Add("path", item["path"]!.GetValue<string>())
                .Add("size_bytes", item["size_bytes"]!.GetValue<long>()).Add("digest", item["digest"]!.GetValue<string>())).ToArray();
        return ContentDigest.Sha256CanonicalJson(new CanonicalJsonObject().Add("paths", CanonicalJsonValue.Array(paths)));
    }

    private static void RewriteRequestEntryAndHead(ResearchWorkspaceLocation location, string exportRoot,
        Action<JsonObject, JsonObject> mutate)
    {
        var requestPath = Path.Combine(exportRoot, WorkspaceExportSchemas.RequestFileName);
        var request = JsonNode.Parse(File.ReadAllBytes(requestPath))!.AsObject();
        var entryPath = Path.Combine(exportRoot, WorkspaceExportSchemas.EntryFileName);
        var entry = JsonNode.Parse(File.ReadAllBytes(entryPath))!.AsObject();
        var content = entry["content"]!.AsObject();
        mutate(request, content);
        var requestBytes = CanonicalBytes(request);
        File.WriteAllBytes(requestPath, requestBytes);
        content["request_digest"] = ContentDigest.Sha256(requestBytes).ToString();
        var entryBytes = CanonicalBytes(entry);
        File.WriteAllBytes(entryPath, entryBytes);
        var headPath = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ExportLedgerHead);
        var head = JsonNode.Parse(File.ReadAllBytes(headPath))!.AsObject();
        head["entry_digest"] = ContentDigest.Sha256(entryBytes).ToString();
        File.WriteAllBytes(headPath, CanonicalBytes(head));
    }

    private static WorkspaceFixture CreateWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nexus-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var location = new ResearchWorkspaceLocation(root, Path.Combine(root, ResearchWorkspacePaths.ProjectFileName));
        var project = ResearchWorkspaceProject.Create("Export test", new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero), "workspace-reporting") with { Revision = 4 };
        ResearchWorkspaceStore.WriteProject(location, project);
        return new WorkspaceFixture(location, project);
    }

    private sealed class WorkspaceFixture(ResearchWorkspaceLocation location, ResearchWorkspaceProject project) : IDisposable
    {
        public ResearchWorkspaceLocation Location { get; } = location;
        public ResearchWorkspaceProject Project { get; } = project;
        public void Dispose()
        {
            if (Directory.Exists(Location.RootDirectory)) Directory.Delete(Location.RootDirectory, true);
        }
    }
}
