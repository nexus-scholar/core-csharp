using System.Text.Json;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Reporting;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ReviewArtifactVerificationCommands
{
    internal static int VerifyReport(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!SingleId(args, error, "Usage: nexus report verify <export-id>", out var exportId))
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        try
        {
            var (location, entry) = ResolveExport(workingDirectory, exportId!);
            var root = ExportRoot(location, entry.ExportId);
            var reportBytes = File.ReadAllBytes(Path.Combine(root, "report.json"));
            var reportVerification = PersistedReportingVerifier.VerifyReport(reportBytes, entry.ReportDigest);
            var request = ReadCanonicalObject(Path.Combine(root, WorkspaceExportSchemas.RequestFileName));
            var sliceDigest = ContentDigest.Parse(RequiredText(request, "slice_digest"));
            if (sliceDigest != reportVerification.SliceDigest)
                throw new InvalidOperationException("Report does not bind the persisted review slice.");
            var sliceBytes = File.ReadAllBytes(Path.Combine(root, "review-slice.json"));
            _ = PersistedReportingVerifier.VerifySlice(sliceBytes, sliceDigest);

            output.WriteLine("Report verification");
            output.WriteLine($"Export: {entry.ExportId}");
            output.WriteLine($"Report digest: {entry.ReportDigest}");
            output.WriteLine($"Slice digest: {sliceDigest}");
            output.WriteLine("Verification: valid persisted canonical bytes and ledger bindings");
            output.WriteLine("Authority replay: not performed");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (Exception exception) when (VerificationFailure(exception))
        {
            error.WriteLine($"Report verification failed: {exception.Message}");
            return ExitCode(exception);
        }
    }

    internal static int VerifyBundle(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!SingleId(args, error, "Usage: nexus bundle verify <export-id>", out var exportId))
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        try
        {
            var (location, entry) = ResolveExport(workingDirectory, exportId!);
            var bundleRoot = Path.Combine(ExportRoot(location, entry.ExportId), "bundle");
            var manifestPath = Path.Combine(bundleRoot, BundleV2Constants.ManifestPath);
            var manifestBytes = File.ReadAllBytes(manifestPath);
            var manifest = ReviewBundleV2CanonicalCodec.Rehydrate(manifestBytes, entry.BundleManifestDigest);
            var inventory = Directory.EnumerateFiles(bundleRoot, "*", SearchOption.AllDirectories)
                .Select(path => new BundleV2ObservedEntry(
                    Path.GetRelativePath(bundleRoot, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes(path)))
                .OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
            var verification = ReviewBundleV2Verifier.Verify(manifest, manifestBytes, inventory);
            if (!verification.IsValid || verification.InventoryDigest != entry.ObservedInventoryDigest)
                throw new WorkspaceExportException(WorkspaceExportErrorCodes.InvalidInventory, "Bundle inventory does not match the export ledger.");

            output.WriteLine("Bundle verification");
            output.WriteLine($"Export: {entry.ExportId}");
            output.WriteLine($"Manifest digest: {verification.ManifestDigest}");
            output.WriteLine($"Inventory digest: {verification.InventoryDigest}");
            output.WriteLine($"Self-contained: {(verification.IsSelfContained ? "yes" : "no")}");
            output.WriteLine("Verification: valid exact Bundle v2 inventory");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (Exception exception) when (VerificationFailure(exception))
        {
            error.WriteLine($"Bundle verification failed: {exception.Message}");
            return ExitCode(exception);
        }
    }

    internal static int VerifyExport(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!SingleId(args, error, "Usage: nexus export verify <export-id>", out var exportId))
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        try
        {
            var (_, entry) = ResolveExport(workingDirectory, exportId!);
            output.WriteLine("Export verification");
            WriteEntry(output, entry);
            output.WriteLine("Verification: valid immutable directory and ledger history");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (Exception exception) when (VerificationFailure(exception))
        {
            error.WriteLine($"Export verification failed: {exception.Message}");
            return ExitCode(exception);
        }
    }

    internal static int ExportStatus(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (args.Length != 0)
        {
            error.WriteLine("Usage: nexus export status");
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        try
        {
            var location = RequireWorkspace(workingDirectory);
            var replay = ResearchWorkspaceExportLedgerVerifier.Replay(location);
            output.WriteLine("Export ledger status");
            output.WriteLine($"Count: {replay.Entries.Count}");
            output.WriteLine($"Head: {replay.Head?.EntryDigest.ToString() ?? "none"}");
            output.WriteLine($"Unreferenced: {replay.UnreferencedExportIds.Count}");
            foreach (var entry in replay.Entries)
                output.WriteLine($"{entry.Ordinal}: {entry.ExportId} {entry.EntrySummary()}");
            output.WriteLine("Verification: complete ledger replay");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (Exception exception) when (VerificationFailure(exception))
        {
            error.WriteLine($"Unable to read export status: {exception.Message}");
            return ExitCode(exception);
        }
    }

    private static (ResearchWorkspaceLocation Location, VerifiedWorkspaceExportLedgerEntry Entry) ResolveExport(
        string workingDirectory, string exportId)
    {
        var location = RequireWorkspace(workingDirectory);
        var replay = ResearchWorkspaceExportLedgerVerifier.Replay(location);
        var entry = replay.Entries.SingleOrDefault(item => item.ExportId == exportId);
        return entry is null
            ? throw new FileNotFoundException($"Export '{exportId}' is not present in the ledger.")
            : (location, entry);
    }

    private static ResearchWorkspaceLocation RequireWorkspace(string workingDirectory) =>
        ResearchWorkspaceStore.FindFrom(workingDirectory) ??
        throw new FileNotFoundException("No Nexus research workspace found in the current folder or its parents.");

    private static string ExportRoot(ResearchWorkspaceLocation location, string exportId) =>
        ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ExportRoot(exportId));

    private static JsonElement ReadCanonicalObject(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
        if (!bytes.SequenceEqual(canonical)) throw new InvalidOperationException("Export request bytes are not canonical.");
        return document.RootElement.Clone();
    }

    private static string RequiredText(JsonElement value, string name) =>
        value.GetProperty(name).GetString() is { Length: > 0 } text ? text : throw new InvalidOperationException($"{name} is required.");

    private static bool SingleId(string[] args, TextWriter error, string usage, out string? exportId)
    {
        exportId = args.Length == 1 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
        if (exportId is not null) return true;
        error.WriteLine(usage);
        return false;
    }

    private static bool VerificationFailure(Exception exception) => exception is
        WorkspaceExportException or BundleV2Exception or ResearchWorkspaceConcurrencyException or
        InvalidOperationException or JsonException or IOException or UnauthorizedAccessException or FormatException or ArgumentException;

    private static int ExitCode(Exception exception) => exception switch
    {
        FileNotFoundException => ResearchWorkspaceExitCodes.MissingProjectOrInput,
        JsonException => ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat,
        _ => ResearchWorkspaceExitCodes.DigestMismatch
    };

    private static void WriteEntry(TextWriter output, VerifiedWorkspaceExportLedgerEntry entry)
    {
        output.WriteLine($"Export: {entry.ExportId}");
        output.WriteLine($"Ordinal: {entry.Ordinal}");
        output.WriteLine($"Entry digest: {entry.Digest}");
        output.WriteLine($"Report digest: {entry.ReportDigest}");
        output.WriteLine($"Bundle manifest digest: {entry.BundleManifestDigest}");
        output.WriteLine($"Inventory digest: {entry.ObservedInventoryDigest}");
        output.WriteLine($"Recorded by: {entry.Actor} ({entry.ActorKind}) at {entry.RecordedAt}");
    }

    private static string EntrySummary(this VerifiedWorkspaceExportLedgerEntry entry) =>
        $"report={entry.ReportDigest} bundle={entry.BundleManifestDigest}";
}
