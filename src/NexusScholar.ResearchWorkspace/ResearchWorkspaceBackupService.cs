using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexusScholar.Kernel;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceBackupService
{
    public const string BackupManifestFileName = "nexus.workspace.backup.manifest.json";
    public const string BackupManifestSchema = "nexus.workspace.backup.manifest";
    public const int BackupManifestVersion = 1;
    public const long MaxBackupRegularFileBytes = 1024L * 1024 * 1024;
    public const long MaxBackupTotalBytes = 20L * 1024 * 1024 * 1024;
    public const int MaxBackupFileCount = 250_000;
    public const long MaxManifestBytes = 16L * 1024 * 1024;

    public static ResearchWorkspaceBackupResult Create(
        string workspaceRoot,
        string destinationArchivePath,
        DateTimeOffset? archiveCreatedAt = null) =>
        CreateCore(workspaceRoot, destinationArchivePath, archiveCreatedAt, null);

    internal static ResearchWorkspaceBackupResult CreateCore(
        string workspaceRoot,
        string destinationArchivePath,
        DateTimeOffset? archiveCreatedAt,
        Action<ResearchWorkspaceBackupCaptureContext>? faultInjector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationArchivePath);

        var location = ResearchWorkspaceStore.FindFrom(workspaceRoot)
            ?? throw new InvalidOperationException("The selected folder does not belong to a Nexus workspace.");
        var workspaceFullPath = Path.GetFullPath(location.RootDirectory);
        var destination = Path.GetFullPath(destinationArchivePath);
        RejectDestinationInsideWorkspace(workspaceFullPath, destination);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new InvalidOperationException("The backup destination must not already exist.");
        }

        var destinationDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            RejectExistingReparseSegments(destinationDirectory);
            Directory.CreateDirectory(destinationDirectory);
        }

        using var workspaceLock = AcquireLock(location);
        var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        _ = ResearchWorkspaceReadModelBuilder.Build(location.RootDirectory);
        var sourceVerification = ResearchWorkspaceVerifier.Verify(location, project);
        RequireStructurallyValidWorkspace(sourceVerification, "The workspace cannot be backed up");
        var records = EnumerateRegularWorkspaceFiles(workspaceFullPath)
            .OrderBy(record => record.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var effectiveArchiveCreatedAt = archiveCreatedAt ?? DateTimeOffset.UtcNow;
        var manifest = BuildManifest(
            project,
            records,
            effectiveArchiveCreatedAt);
        var zipTimestamp = NormalizeZipTimestamp(effectiveArchiveCreatedAt);

        var temporaryPath = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                foreach (var record in records)
                {
                    using var source = new FileStream(
                        record.AbsolutePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);
                    var entry = archive.CreateEntry(record.RelativePath, CompressionLevel.Optimal);
                    entry.LastWriteTime = zipTimestamp;
                    entry.ExternalAttributes = 0;
                    using var target = entry.Open();
                    source.CopyTo(target);
                    faultInjector?.Invoke(new ResearchWorkspaceBackupCaptureContext(
                        record.RelativePath,
                        record.AbsolutePath,
                        ResearchWorkspaceBackupCapturePoint.AfterFileWritten));
                }

                var manifestEntry = archive.CreateEntry(BackupManifestFileName, CompressionLevel.Optimal);
                manifestEntry.LastWriteTime = zipTimestamp;
                manifestEntry.ExternalAttributes = 0;
                using var manifestStream = manifestEntry.Open();
                var manifestBytes = SerializeManifest(manifest);
                manifestStream.Write(manifestBytes, 0, manifestBytes.Length);
            }

            faultInjector?.Invoke(new ResearchWorkspaceBackupCaptureContext(
                string.Empty,
                string.Empty,
                ResearchWorkspaceBackupCapturePoint.BeforeCompletionVerification));

            VerifyCompletedBackupArchive(temporaryPath, workspaceFullPath, manifest);

            File.Move(temporaryPath, destination);
            return new ResearchWorkspaceBackupResult(destination, manifest.ManifestDigest, manifest);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static ResearchWorkspaceRestoreResult Restore(
        string backupArchivePath,
        string targetWorkspaceRoot) =>
        RestoreCore(backupArchivePath, targetWorkspaceRoot, null);

    internal static ResearchWorkspaceRestoreResult RestoreCore(
        string backupArchivePath,
        string targetWorkspaceRoot,
        Action<ResearchWorkspaceRestoreContext>? faultInjector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupArchivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetWorkspaceRoot);
        if (!File.Exists(backupArchivePath))
        {
            throw new FileNotFoundException("Backup archive does not exist.", backupArchivePath);
        }
        if ((File.GetAttributes(backupArchivePath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The backup archive cannot be a linked file.");
        }
        var archiveParent = Path.GetDirectoryName(Path.GetFullPath(backupArchivePath));
        if (!string.IsNullOrWhiteSpace(archiveParent))
        {
            RejectExistingReparseSegments(archiveParent);
        }

        var targetRoot = Path.GetFullPath(targetWorkspaceRoot);
        if (File.Exists(targetRoot) || Directory.Exists(targetRoot))
        {
            throw new InvalidOperationException("The restore target must not exist.");
        }

        var targetParent = Path.GetDirectoryName(targetRoot);
        if (!string.IsNullOrWhiteSpace(targetParent))
        {
            RejectExistingReparseSegments(targetParent);
            var existingParent = FindExistingDirectoryAncestor(targetParent);
            if (existingParent is not null &&
                ResearchWorkspaceStore.FindFrom(existingParent) is not null)
            {
                throw new InvalidOperationException(
                    "The restore target cannot be inside an existing Nexus workspace.");
            }
            Directory.CreateDirectory(targetParent);
        }

        var stagingRoot = $"{targetRoot}.restore-staging";
        if (File.Exists(stagingRoot) || Directory.Exists(stagingRoot))
        {
            throw new InvalidOperationException("The restore staging directory already exists.");
        }

        try
        {
            using var archive = ZipFile.OpenRead(backupArchivePath);
            var manifest = ParseManifest(ReadManifestBytes(archive));
            var entries = ReadEntriesByName(archive);
            VerifyManifestAndInventory(manifest, entries);

            Directory.CreateDirectory(stagingRoot);

            try
            {
                foreach (var file in manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
                {
                    var destinationPath = Path.Combine(stagingRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    var destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    using (var source = entries[file.RelativePath].Open())
                    using (var destination = new FileStream(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        source.CopyTo(destination);
                    }

                    var restoredFile = new FileInfo(destinationPath);
                    if (restoredFile.Length != file.SizeBytes || FileSha256(destinationPath) != file.Sha256)
                    {
                        throw new InvalidOperationException($"The restored file '{file.RelativePath}' does not match its manifest digest.");
                    }

                    faultInjector?.Invoke(new ResearchWorkspaceRestoreContext(
                        file.RelativePath,
                        destinationPath,
                        ResearchWorkspaceRestorePoint.AfterFileWritten));
                }

                foreach (var relativeDirectory in ResearchWorkspacePaths.RequiredDirectories)
                {
                    Directory.CreateDirectory(ResearchWorkspacePaths.InProject(stagingRoot, relativeDirectory));
                }

                var projectPath = Path.Combine(stagingRoot, ResearchWorkspacePaths.ProjectFileName);
                var project = ResearchWorkspaceStore.ReadProject(projectPath);
                if (!string.Equals(project.WorkspaceId, manifest.WorkspaceId, StringComparison.Ordinal) ||
                    project.Revision != manifest.ProjectRevision ||
                    !string.Equals(project.CreatedAt, manifest.WorkspaceCreatedAtUtc, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The restored workspace metadata does not match the backup manifest.");
                }

                var location = new ResearchWorkspaceLocation(stagingRoot, projectPath);
                var restoreVerification = ResearchWorkspaceVerifier.Verify(location, project);
                RequireStructurallyValidWorkspace(restoreVerification, "The restored workspace failed verification");
                _ = ResearchWorkspaceGenerationVerifier.VerifyCurrent(location, project);
                _ = ResearchWorkspaceReadModelBuilder.Build(stagingRoot);

                faultInjector?.Invoke(new ResearchWorkspaceRestoreContext(
                    string.Empty,
                    stagingRoot,
                    ResearchWorkspaceRestorePoint.BeforePromotion));

                Directory.Move(stagingRoot, targetRoot);
                return new ResearchWorkspaceRestoreResult(targetRoot, project.WorkspaceId, project.Revision);
            }
            catch
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, recursive: true);
                }

                throw;
            }
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static byte[] SerializeManifest(ResearchWorkspaceBackupManifest manifest)
    {
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(
            new CanonicalJsonObject()
                .Add("schema", manifest.Schema)
                .Add("schema_version", manifest.SchemaVersion)
                .Add("workspace_id", manifest.WorkspaceId)
                .Add("project_revision", manifest.ProjectRevision)
                .Add("workspace_created_at", manifest.WorkspaceCreatedAtUtc)
                .Add("archive_created_at", manifest.ArchiveCreatedAtUtc)
                .Add("total_bytes", manifest.TotalBytes)
                .Add("file_count", manifest.FileCount)
                .Add("manifest_digest", manifest.ManifestDigest)
                .Add("files", CanonicalJsonValue.Array(manifest.Files
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => (CanonicalJsonValue)new CanonicalJsonObject()
                        .Add("path", file.RelativePath)
                        .Add("size", file.SizeBytes)
                        .Add("sha256", file.Sha256))
                    .ToArray())));
    }

    private static byte[] SerializeManifestForDigest(
        string workspaceId,
        long projectRevision,
        string workspaceCreatedAtUtc,
        string archiveCreatedAtUtc,
        IReadOnlyList<ResearchWorkspaceBackupManifestFile> files)
    {
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(
            new CanonicalJsonObject()
                .Add("schema", BackupManifestSchema)
                .Add("schema_version", BackupManifestVersion)
                .Add("workspace_id", workspaceId)
                .Add("project_revision", projectRevision)
                .Add("workspace_created_at", workspaceCreatedAtUtc)
                .Add("archive_created_at", archiveCreatedAtUtc)
                .Add("total_bytes", files.Sum(file => file.SizeBytes))
                .Add("file_count", files.Count)
                .Add("files", CanonicalJsonValue.Array(files
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => (CanonicalJsonValue)new CanonicalJsonObject()
                        .Add("path", file.RelativePath)
                        .Add("size", file.SizeBytes)
                        .Add("sha256", file.Sha256))
                    .ToArray())));
    }

    private static ResearchWorkspaceBackupManifest BuildManifest(
        ResearchWorkspaceProject project,
        IReadOnlyList<ResearchWorkspaceBackupRecord> records,
        DateTimeOffset archiveCreatedAt)
    {
        var files = records
            .Select(record => new ResearchWorkspaceBackupManifestFile(record.RelativePath, record.SizeBytes, record.Sha256))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();

        var archiveCreatedAtUtc = archiveCreatedAt.UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture);
        var totalBytes = files.Sum(file => file.SizeBytes);
        var withDigest = new ResearchWorkspaceBackupManifest(
            BackupManifestSchema,
            BackupManifestVersion,
            project.WorkspaceId,
            project.Revision,
            project.CreatedAt,
            archiveCreatedAtUtc,
            totalBytes,
            files.Length,
            files,
            string.Empty);

        var digest = ContentDigest.Sha256(SerializeManifestForDigest(
            withDigest.WorkspaceId,
            withDigest.ProjectRevision,
            withDigest.WorkspaceCreatedAtUtc,
            withDigest.ArchiveCreatedAtUtc,
            files));
        return withDigest with
        {
            ManifestDigest = digest.ToString()
        };
    }

    private static ResearchWorkspaceBackupManifest ParseManifest(byte[] manifestBytes)
    {
        var manifest = JsonSerializer.Deserialize<ResearchWorkspaceBackupManifest>(manifestBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Backup manifest content was empty.");

        if (!string.Equals(manifest.Schema, BackupManifestSchema, StringComparison.Ordinal) ||
            manifest.SchemaVersion != BackupManifestVersion ||
            manifest.FileCount != manifest.Files.Count ||
            manifest.TotalBytes < 0)
        {
            throw new InvalidOperationException("The backup manifest is malformed.");
        }

        if (string.IsNullOrWhiteSpace(manifest.WorkspaceId) ||
            manifest.FileCount == 0 ||
            manifest.Files.Count == 0 && manifest.TotalBytes != 0)
        {
            throw new InvalidOperationException("The backup manifest is malformed.");
        }

        if (!DateTimeOffset.TryParseExact(
                manifest.WorkspaceCreatedAtUtc,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out _))
        {
            throw new InvalidOperationException("The backup manifest is malformed.");
        }
        if (!DateTimeOffset.TryParseExact(
                manifest.ArchiveCreatedAtUtc,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out _) ||
            manifest.ProjectRevision < 0 ||
            manifest.FileCount > MaxBackupFileCount ||
            manifest.TotalBytes > MaxBackupTotalBytes)
        {
            throw new InvalidOperationException("The backup manifest is malformed.");
        }

        var normalizedFiles = new List<ResearchWorkspaceBackupManifestFile>(manifest.Files.Count);
        var seenPaths = new HashSet<string>(PathComparer);

        foreach (var file in manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            if (!ContentDigest.TryParse(file.Sha256, out _))
            {
                throw new InvalidOperationException($"The backup manifest file digest is invalid: {file.RelativePath}");
            }

            if (file.SizeBytes < 0 || file.SizeBytes > MaxBackupRegularFileBytes)
            {
                throw new InvalidOperationException($"The backup manifest contains an oversized file: {file.RelativePath}");
            }

            var normalizedPath = NormalizeManifestPath(file.RelativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                !string.Equals(file.RelativePath, normalizedPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The backup manifest contains an invalid file path.");
            }

            if (!seenPaths.Add(normalizedPath))
            {
                throw new InvalidOperationException($"The backup manifest has a duplicate path: {normalizedPath}");
            }

            normalizedFiles.Add(file with { RelativePath = normalizedPath });
        }

        var validatedManifest = manifest with
        {
            Files = normalizedFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray()
        };
        var expectedDigest = ContentDigest.Sha256(SerializeManifestForDigest(
            validatedManifest.WorkspaceId,
            validatedManifest.ProjectRevision,
            validatedManifest.WorkspaceCreatedAtUtc,
            validatedManifest.ArchiveCreatedAtUtc,
            validatedManifest.Files)).ToString();

        if (!string.Equals(expectedDigest, validatedManifest.ManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup manifest digest does not match its content.");
        }

        validatedManifest = validatedManifest with
        {
            FileCount = validatedManifest.Files.Count,
            TotalBytes = validatedManifest.Files.Sum(file => file.SizeBytes)
        };

        if (validatedManifest.TotalBytes != manifest.TotalBytes || validatedManifest.FileCount != manifest.FileCount)
        {
            throw new InvalidOperationException("The backup manifest metadata is inconsistent.");
        }

        return validatedManifest;
    }

    private static Dictionary<string, ZipArchiveEntry> ReadEntriesByName(ZipArchive archive)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(PathComparer);

        foreach (var entry in archive.Entries)
        {
            if (string.Equals(entry.FullName, BackupManifestFileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.FullName.Length == 0)
            {
                throw new InvalidOperationException("The backup archive contains an unnamed entry.");
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The backup archive contains an invalid directory entry: {entry.FullName}.");
            }
            if (IsLinkedArchiveEntry(entry))
            {
                throw new InvalidOperationException($"The backup archive contains a linked entry: {entry.FullName}.");
            }

            var normalized = NormalizeManifestPath(entry.FullName);
            if (string.IsNullOrWhiteSpace(normalized) ||
                !string.Equals(entry.FullName, normalized, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The backup archive entry path is unsafe: {entry.FullName}.");
            }

            if (entries.ContainsKey(normalized))
            {
                throw new InvalidOperationException($"The backup archive contains duplicate entries: {normalized}.");
            }

            entries[normalized] = entry;
        }
        return entries;
    }

    private static byte[] ReadManifestBytes(ZipArchive archive)
    {
        var manifests = archive.Entries
            .Where(entry => string.Equals(entry.FullName, BackupManifestFileName, StringComparison.Ordinal))
            .ToArray();
        if (manifests.Length != 1)
        {
            throw new InvalidOperationException("The backup archive must contain exactly one manifest.");
        }

        var entry = manifests[0];
        if (entry.Length < 1 || entry.Length > MaxManifestBytes)
        {
            throw new InvalidOperationException("The backup archive manifest size is invalid.");
        }
        using var stream = entry.Open();
        using var manifestBytes = new MemoryStream();
        stream.CopyTo(manifestBytes);
        return manifestBytes.ToArray();
    }

    private static void VerifyManifestAndInventory(
        ResearchWorkspaceBackupManifest manifest,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        var expected = manifest.Files.Select(file => file.RelativePath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var observed = entries.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(observed, PathComparer))
        {
            throw new InvalidOperationException("The backup archive file inventory does not match the manifest.");
        }

        foreach (var file in manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            var entry = entries[file.RelativePath];
            if (entry.Length != file.SizeBytes)
            {
                throw new InvalidOperationException($"The backup archive entry size does not match the manifest: {file.RelativePath}.");
            }

            using var stream = entry.Open();
            if (StreamSha256(stream) != file.Sha256)
            {
                throw new InvalidOperationException($"The backup archive entry digest does not match the manifest: {file.RelativePath}.");
            }
        }
    }

    private static void VerifyCompletedBackupArchive(
        string temporaryPath,
        string workspaceRoot,
        ResearchWorkspaceBackupManifest expectedManifest)
    {
        using var archive = ZipFile.OpenRead(temporaryPath);
        var manifest = ParseManifest(ReadManifestBytes(archive));
        if (manifest.Schema != expectedManifest.Schema ||
            manifest.SchemaVersion != expectedManifest.SchemaVersion ||
            manifest.WorkspaceId != expectedManifest.WorkspaceId ||
            manifest.ProjectRevision != expectedManifest.ProjectRevision ||
            manifest.WorkspaceCreatedAtUtc != expectedManifest.WorkspaceCreatedAtUtc ||
            manifest.ManifestDigest != expectedManifest.ManifestDigest ||
            manifest.Files.Count != expectedManifest.Files.Count ||
            manifest.TotalBytes != expectedManifest.TotalBytes ||
            manifest.FileCount != expectedManifest.FileCount)
        {
            throw new InvalidOperationException("The backup archive manifest changed after completion.");
        }

        var entries = ReadEntriesByName(archive);
        VerifyManifestAndInventory(manifest, entries);

        var currentRecords = EnumerateRegularWorkspaceFiles(workspaceRoot)
            .OrderBy(record => record.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (currentRecords.Length != expectedManifest.Files.Count)
        {
            throw new InvalidOperationException("The backup source file inventory changed during archive write.");
        }
        for (var index = 0; index < currentRecords.Length; index++)
        {
            var current = currentRecords[index];
            var expected = expectedManifest.Files[index];
            if (!string.Equals(current.RelativePath, expected.RelativePath, StringComparison.Ordinal) ||
                current.SizeBytes != expected.SizeBytes ||
                !string.Equals(current.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The backup source changed during archive write: {expected.RelativePath}.");
            }
        }
    }

    private static IReadOnlyList<ResearchWorkspaceBackupRecord> EnumerateRegularWorkspaceFiles(string workspaceRoot)
    {
        var root = Path.GetFullPath(workspaceRoot);
        var lockPath = Path.Combine(root, ResearchWorkspacePaths.ProjectLockFileName);
        var records = new List<ResearchWorkspaceBackupRecord>();
        var seen = new HashSet<string>(PathComparer);

        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("The workspace root does not exist.");
        }

        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var directory = stack.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var absolutePath = Path.GetFullPath(entry);
                var attributes = File.GetAttributes(absolutePath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"The backup path is unsafe or linked: {Path.GetRelativePath(root, absolutePath)}");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    stack.Push(absolutePath);
                    continue;
                }

                if (string.Equals(absolutePath, lockPath, PathComparison))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(root, absolutePath).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(relativePath) ||
                    !string.Equals(relativePath, NormalizeManifestPath(relativePath), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"The backup path is not workspace-safe: {absolutePath}");
                }

                if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(root, relativePath, out var resolvedPath) ||
                    !PathsEqual(resolvedPath, absolutePath))
                {
                    throw new InvalidOperationException($"The backup path is unsafe: {relativePath}");
                }

                if (!seen.Add(relativePath))
                {
                    throw new InvalidOperationException($"The backup path is duplicated: {relativePath}");
                }

                var info = new FileInfo(absolutePath);
                if (info.Length < 0 || info.Length > MaxBackupRegularFileBytes)
                {
                    throw new InvalidOperationException($"The backup source file exceeded the manifest limit: {relativePath}");
                }

                records.Add(new ResearchWorkspaceBackupRecord(
                    relativePath,
                    absolutePath,
                    info.Length,
                    FileSha256(absolutePath)));
            }
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException("The workspace has no admissible files to back up.");
        }
        if (records.Count > MaxBackupFileCount ||
            records.Sum(record => record.SizeBytes) > MaxBackupTotalBytes)
        {
            throw new InvalidOperationException("The workspace exceeds the admitted backup size or file-count limit.");
        }

        return records;
    }

    private static bool PathsEqual(string pathA, string pathB)
    {
        return string.Equals(
            Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            PathComparison);
    }

    private static string NormalizeManifestPath(string rawPath)
    {
        var normalized = rawPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.IndexOf(':') >= 0 ||
            Path.IsPathRooted(normalized))
        {
            return string.Empty;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Contains(".", StringComparer.Ordinal) ||
            segments.Contains("..", StringComparer.Ordinal))
        {
            return string.Empty;
        }

        return string.Join('/', segments);
    }

    private static string FileSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return StreamSha256(stream);
    }

    private static string StreamSha256(Stream stream) =>
        ContentDigest.Create(
            DigestAlgorithm.Sha256,
            Convert.ToHexStringLower(SHA256.HashData(stream))).ToString();

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static DateTimeOffset NormalizeZipTimestamp(DateTimeOffset timestamp)
    {
        var utc = timestamp.ToUniversalTime();
        if (utc.Year < 1980)
        {
            utc = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }
        else if (utc.Year > 2107)
        {
            utc = new DateTimeOffset(2107, 12, 31, 23, 59, 58, TimeSpan.Zero);
        }

        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            utc.Second - utc.Second % 2,
            TimeSpan.Zero);
    }

    private static bool IsLinkedArchiveEntry(ZipArchiveEntry entry)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        var unixMode = (entry.ExternalAttributes >> 16) & unixFileTypeMask;
        var windowsAttributes = (FileAttributes)(entry.ExternalAttributes & 0xFFFF);
        return unixMode == unixSymbolicLink ||
            (windowsAttributes & FileAttributes.ReparsePoint) != 0;
    }

    private static void RejectExistingReparseSegments(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null && current.Exists)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("The selected path contains a linked directory.");
            }

            current = current.Parent;
        }
    }

    private static string? FindExistingDirectoryAncestor(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (!current.Exists)
        {
            current = current.Parent;
            if (current is null)
            {
                return null;
            }
        }

        return current.FullName;
    }

    private static void RequireStructurallyValidWorkspace(
        ResearchWorkspaceVerificationReport report,
        string prefix)
    {
        if (report.MissingFiles.Count > 0 ||
            report.DigestMismatches.Count > 0 ||
            report.InvalidPaths.Count > 0 ||
            report.MissingImportTraces.Count > 0)
        {
            throw new InvalidOperationException(
                $"{prefix}: missing files {report.MissingFiles.Count}, digest mismatches {report.DigestMismatches.Count}, " +
                $"invalid paths {report.InvalidPaths.Count}, missing import traces {report.MissingImportTraces.Count}.");
        }
    }

    private static void RejectDestinationInsideWorkspace(string workspaceRoot, string destinationPath)
    {
        var workspace = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var destination = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var workspaceWithSeparator = workspace + Path.DirectorySeparatorChar;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(workspace, destination, comparison) || destination.StartsWith(workspaceWithSeparator, comparison))
        {
            throw new InvalidOperationException("The backup destination cannot be inside the workspace root.");
        }
    }

    private static FileStream AcquireLock(ResearchWorkspaceLocation location)
    {
        var path = Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName);
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new ResearchWorkspaceConcurrencyException("The workspace is locked by another mutation.", exception);
        }
    }
}

internal sealed record ResearchWorkspaceBackupCaptureContext(
    string RelativePath,
    string AbsolutePath,
    ResearchWorkspaceBackupCapturePoint Point);

internal enum ResearchWorkspaceBackupCapturePoint
{
    AfterFileWritten,
    BeforeCompletionVerification
}

internal sealed record ResearchWorkspaceRestoreContext(
    string RelativePath,
    string DestinationPath,
    ResearchWorkspaceRestorePoint Point);

internal enum ResearchWorkspaceRestorePoint
{
    AfterFileWritten,
    BeforePromotion
}

public sealed record ResearchWorkspaceBackupResult(
    string ArchivePath,
    string ManifestDigest,
    ResearchWorkspaceBackupManifest Manifest);

public sealed record ResearchWorkspaceRestoreResult(
    string WorkspaceRoot,
    string WorkspaceId,
    long ProjectRevision);

public sealed record ResearchWorkspaceBackupManifestFile(
    [property: JsonPropertyName("path")] string RelativePath,
    [property: JsonPropertyName("size")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record ResearchWorkspaceBackupManifest(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("project_revision")] long ProjectRevision,
    [property: JsonPropertyName("workspace_created_at")] string WorkspaceCreatedAtUtc,
    [property: JsonPropertyName("archive_created_at")] string ArchiveCreatedAtUtc,
    [property: JsonPropertyName("total_bytes")] long TotalBytes,
    [property: JsonPropertyName("file_count")] int FileCount,
    [property: JsonPropertyName("files")] IReadOnlyList<ResearchWorkspaceBackupManifestFile> Files,
    [property: JsonPropertyName("manifest_digest")] string ManifestDigest);

public sealed record ResearchWorkspaceBackupRecord(
    string RelativePath,
    string AbsolutePath,
    long SizeBytes,
    string Sha256);
