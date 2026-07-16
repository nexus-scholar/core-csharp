using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Reporting;

namespace NexusScholar.AppServices;

public sealed record ReviewExportSourceBinding(
    string Role,
    string GenerationId,
    ContentDigest ManifestDigest,
    string? CandidateId);

public static class ReviewExportActorKinds
{
    public const string Human = "human";
    public const string Automation = "automation";
}

public sealed record ReviewExportActor(string ActorId, string ActorKind);

public sealed class VerifiedReviewExportRequest
{
    private readonly byte[] _reportBytes;
    private readonly byte[] _sliceBytes;
    private readonly byte[] _reportMarkdownBytes;
    private readonly byte[] _bundleManifestBytes;
    private readonly BundleV2ObservedEntry[] _observedInventory;
    private readonly byte[] _requestBytes;

    internal VerifiedReviewExportRequest(
        string exportId,
        string actor,
        string actorKind,
        string occurredAt,
        string workspaceId,
        long projectRevision,
        ContentDigest reportDigest,
        ContentDigest workspaceCutDigest,
        ContentDigest bundleManifestDigest,
        ContentDigest observedInventoryDigest,
        IReadOnlyList<ReviewExportSourceBinding> sources,
        byte[] reportBytes,
        byte[] sliceBytes,
        byte[] reportMarkdownBytes,
        byte[] bundleManifestBytes,
        IReadOnlyList<BundleV2ObservedEntry> observedInventory,
        byte[] requestBytes,
        ContentDigest requestDigest)
    {
        ExportId = exportId;
        Actor = actor;
        ActorKind = actorKind;
        OccurredAt = occurredAt;
        WorkspaceId = workspaceId;
        ProjectRevision = projectRevision;
        ReportDigest = reportDigest;
        WorkspaceCutDigest = workspaceCutDigest;
        BundleManifestDigest = bundleManifestDigest;
        ObservedInventoryDigest = observedInventoryDigest;
        Sources = Array.AsReadOnly(sources.Select(item => item with { }).ToArray());
        _reportBytes = reportBytes.ToArray();
        _sliceBytes = sliceBytes.ToArray();
        _reportMarkdownBytes = reportMarkdownBytes.ToArray();
        _bundleManifestBytes = bundleManifestBytes.ToArray();
        _observedInventory = observedInventory.Select(item => new BundleV2ObservedEntry(item.Path, item.Bytes.ToArray())).ToArray();
        _requestBytes = requestBytes.ToArray();
        RequestDigest = requestDigest;
    }

    public string ExportId { get; }
    public string Actor { get; }
    public string ActorKind { get; }
    public string OccurredAt { get; }
    public string WorkspaceId { get; }
    public long ProjectRevision { get; }
    public ContentDigest ReportDigest { get; }
    public ContentDigest WorkspaceCutDigest { get; }
    public ContentDigest BundleManifestDigest { get; }
    public ContentDigest ObservedInventoryDigest { get; }
    public IReadOnlyList<ReviewExportSourceBinding> Sources { get; }
    public byte[] ReportBytes => _reportBytes.ToArray();
    public byte[] SliceBytes => _sliceBytes.ToArray();
    public byte[] ReportMarkdownBytes => _reportMarkdownBytes.ToArray();
    public byte[] BundleManifestBytes => _bundleManifestBytes.ToArray();
    public IReadOnlyList<BundleV2ObservedEntry> ObservedInventory => Array.AsReadOnly(_observedInventory
        .Select(item => new BundleV2ObservedEntry(item.Path, item.Bytes.ToArray())).ToArray());
    public byte[] RequestBytes => _requestBytes.ToArray();
    public ContentDigest RequestDigest { get; }
}

public sealed record ReviewExportCommitResult(
    string ExportId,
    ContentDigest RequestDigest,
    ContentDigest EntryDigest,
    ContentDigest ObservedInventoryDigest,
    long Ordinal,
    bool AlreadyApplied);

public interface IReviewExportCommitPort
{
    ReviewExportCommitResult Commit(VerifiedReviewExportRequest request, ContentDigest? expectedPreviousEntryDigest);
}

public static class ReviewExportApplicationService
{
    public static ReviewExportCommitResult Commit(
        VerifiedReviewExportRequest request,
        ContentDigest? expectedPreviousEntryDigest,
        IReviewExportCommitPort port)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(port);
        var result = port.Commit(request, expectedPreviousEntryDigest);
        if (result.ExportId != request.ExportId || result.RequestDigest != request.RequestDigest ||
            result.ObservedInventoryDigest != request.ObservedInventoryDigest || result.Ordinal <= 0 || !result.EntryDigest.IsValid)
            throw new InvalidOperationException("Export commit result does not match the verified request.");
        return result;
    }
}

public static partial class ReviewExportOrchestrator
{
    public static VerifiedReviewExportRequest Prepare(
        string exportId,
        ReviewExportActor actor,
        string occurredAt,
        VerifiedReviewFlowReport report,
        VerifiedReviewBundleV2 bundle,
        byte[] bundleManifestBytes,
        IEnumerable<BundleV2ObservedEntry> observedInventory)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(bundleManifestBytes);
        var id = Required(exportId, nameof(exportId));
        if (!SafeIdentifier().IsMatch(id) || id.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Export id must be a safe identifier.");
        ArgumentNullException.ThrowIfNull(actor);
        var human = Required(actor.ActorId, nameof(actor));
        if (actor.ActorKind != ReviewExportActorKinds.Human)
            throw new InvalidOperationException("Only an identified human actor may record a workspace export.");
        if (!DateTimeOffset.TryParseExact(occurredAt, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
            throw new InvalidOperationException("Export time must be a UTC timestamp with second precision.");

        var reportBytes = ReportingCanonicalCodec.SerializeReport(report);
        var sliceBytes = ReportingCanonicalCodec.SerializeSlice(report);
        var markdownBytes = ReviewFlowMarkdownRenderer.Render(report);
        var inventory = (observedInventory ?? throw new ArgumentNullException(nameof(observedInventory)))
            .Select(item => new BundleV2ObservedEntry(item.Path, item.Bytes.ToArray())).ToArray();
        var verification = ReviewBundleV2Verifier.Verify(bundle, bundleManifestBytes, inventory);
        if (!verification.IsValid)
            throw new InvalidOperationException("Export requires a valid exact Bundle v2 inventory.");
        if (bundle.ReportDigest.Scope != DigestScope.CanonicalJsonRecord.ToString() ||
            bundle.ReportDigest.Value != report.ReportDigest ||
            bundle.WorkspaceCutDigest.Scope != DigestScope.CanonicalJsonRecord.ToString() ||
            bundle.WorkspaceCutDigest.Value != report.WorkspaceCut.Digest ||
            bundle.WorkspaceId != report.WorkspaceCut.WorkspaceId ||
            bundle.ProjectRevision != report.WorkspaceCut.ProjectRevision)
            throw new InvalidOperationException("Report, workspace cut, and Bundle v2 authorities do not match.");
        var cutSources = report.WorkspaceCut.Generations.Select(item =>
            (item.Role, item.GenerationId, item.ManifestDigest, item.CandidateId)).ToArray();
        var bundleSources = bundle.SourceGenerations.Select(item =>
            (item.Role, item.GenerationId, item.ManifestDigest.Value, item.CandidateId)).ToArray();
        if (!cutSources.SequenceEqual(bundleSources))
            throw new InvalidOperationException("Bundle source generations do not reproduce the verified report workspace cut.");

        var canonicalReportEntries = bundle.Entries.OfType<BundleV2EmbeddedEntry>()
            .Where(item => item.ArtifactRole == "canonical-report").ToArray();
        if (canonicalReportEntries.Length != 1 ||
            !inventory.Any(item => item.Path == canonicalReportEntries[0].LogicalPath && item.Bytes.SequenceEqual(reportBytes)))
            throw new InvalidOperationException("Bundle inventory must contain the exact canonical report bytes.");

        var sources = bundle.SourceGenerations.Select(item => new ReviewExportSourceBinding(
            item.Role, item.GenerationId, item.ManifestDigest.Value, item.CandidateId)).ToArray();
        var content = new CanonicalJsonObject()
            .Add("export_id", id)
            .Add("actor_id", human)
            .Add("actor_kind", actor.ActorKind)
            .Add("recorded_at", occurredAt)
            .Add("workspace_id", bundle.WorkspaceId)
            .Add("project_revision", bundle.ProjectRevision)
            .Add("report_digest", report.ReportDigest.ToString())
            .Add("workspace_cut_digest", report.WorkspaceCut.Digest.ToString())
            .Add("bundle_manifest_digest", verification.ManifestDigest.ToString())
            .Add("observed_inventory_digest", verification.InventoryDigest.ToString())
            .Add("slice_digest", ContentDigest.Sha256(sliceBytes).ToString())
            .Add("report_markdown_digest", ContentDigest.Sha256(markdownBytes).ToString())
            .Add("artifacts", CanonicalJsonValue.Array(inventory.OrderBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => new CanonicalJsonObject().Add("path", item.Path).Add("size_bytes", item.Bytes.LongLength)
                    .Add("digest", ContentDigest.Sha256(item.Bytes).ToString())).ToArray()))
            .Add("source_generations", CanonicalJsonValue.Array(sources.Select(SourceJson).ToArray()));
        var requestBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(content);
        var requestDigest = ContentDigest.Sha256(requestBytes);
        return new VerifiedReviewExportRequest(id, human, actor.ActorKind, occurredAt, bundle.WorkspaceId, bundle.ProjectRevision,
            report.ReportDigest, report.WorkspaceCut.Digest, verification.ManifestDigest, verification.InventoryDigest,
            sources, reportBytes, sliceBytes, markdownBytes, bundleManifestBytes, inventory, requestBytes, requestDigest);
    }

    private static CanonicalJsonObject SourceJson(ReviewExportSourceBinding source)
    {
        var value = new CanonicalJsonObject().Add("role", source.Role).Add("generation_id", source.GenerationId)
            .Add("manifest_digest", source.ManifestDigest.ToString());
        if (source.CandidateId is not null) value.Add("candidate_id", source.CandidateId);
        return value;
    }

    private static string Required(string value, string name) =>
        !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException("Value is required.", name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifier();
}
