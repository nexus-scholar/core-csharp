using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;

namespace NexusScholar.ResearchWorkspace.Tests;

[TestClass]
public sealed class ResearchWorkspaceBackupServiceTests
{
    [TestMethod]
    public void Backup_and_restore_roundtrip_preserves_file_bytes()
    {
        using var workspace = TemporaryWorkspace.Create();
        var fileA = Path.Combine(workspace.Root, "inputs", "search", "backup-a.csv");
        var fileB = Path.Combine(workspace.Root, ResearchWorkspacePaths.DedupOutputs, "backup-b.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(fileA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(fileB)!);
        File.WriteAllText(fileA, "alpha,beta,gamma", Encoding.UTF8);
        File.WriteAllText(fileB, "beta", Encoding.UTF8);

        var project = workspace.Project.WithInput(new ResearchWorkspaceInput
        {
            InputId = "sample-input",
            Kind = "search-export",
            Source = "scopus",
            Format = "csv",
            RelativePath = "inputs/search/backup-a.csv",
            Sha256 = Sha256(File.ReadAllBytes(fileA)),
            QueryId = "sample-input",
            QueryText = "sample",
            ImportTracePath = "nexus-output/imports/backup-a.import-trace.json"
        });
        WriteTextFile(workspace.Root, "nexus-output/imports/backup-a.import-trace.json", "{}");
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);

        var backupPath = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-roundtrip-{Guid.NewGuid():N}.zip");
        var createResult = ResearchWorkspaceBackupService.Create(workspace.Root, backupPath, BackupTime);
        Assert.IsTrue(File.Exists(createResult.ArchivePath), "Backup archive was not created.");
        var projectManifestEntry = createResult.Manifest.Files.Single(file =>
            file.RelativePath == ResearchWorkspacePaths.ProjectFileName);
        Assert.AreEqual(
            ContentDigest.Sha256(File.ReadAllBytes(workspace.Location.ProjectFilePath)).ToString(),
            projectManifestEntry.Sha256);
        var restoredWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-roundtrip-target-{Guid.NewGuid():N}");

        try
        {
            var restoreResult = ResearchWorkspaceBackupService.Restore(backupPath, restoredWorkspaceRoot);
            Assert.AreEqual(createResult.Manifest.WorkspaceId, restoreResult.WorkspaceId);

            var sourceFiles = SnapshotRegularFiles(workspace.Root)
                .Where(path => !string.Equals(path, ResearchWorkspacePaths.ProjectLockFileName, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            var restoredFiles = SnapshotRegularFiles(restoredWorkspaceRoot)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(sourceFiles, restoredFiles, "The restored workspace changed its file set.");

            foreach (var relativePath in sourceFiles)
            {
                var left = File.ReadAllBytes(Path.Combine(workspace.Root, relativePath));
                var right = File.ReadAllBytes(Path.Combine(restoredWorkspaceRoot, relativePath));
                CollectionAssert.AreEqual(left, right, $"File bytes changed for '{relativePath}'.");
            }

            foreach (var relativeDirectory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Assert.IsTrue(
                    Directory.Exists(ResearchWorkspacePaths.InProject(restoredWorkspaceRoot, relativeDirectory)),
                    $"Restore did not recreate required workspace directory '{relativeDirectory}'.");
            }
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            if (Directory.Exists(restoredWorkspaceRoot))
            {
                Directory.Delete(restoredWorkspaceRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Restore_rejects_tampered_archive_entry()
    {
        using var workspace = TemporaryWorkspace.Create();
        WriteTextFile(workspace.Root, "nexus.txt", "before");
        ResearchWorkspaceStore.WriteProject(workspace.Location, workspace.Project);

        var sourceBackup = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-tamper-{Guid.NewGuid():N}.zip");
        var tamperedBackup = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-tamper-target-{Guid.NewGuid():N}.zip");

        var result = ResearchWorkspaceBackupService.Create(workspace.Root, sourceBackup, BackupTime);
        using (var archive = ZipFile.Open(tamperedBackup, ZipArchiveMode.Create))
        {
            using var source = ZipFile.OpenRead(sourceBackup);
            foreach (var entry in source.Entries)
            {
                var copied = archive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var sourceStream = entry.Open();
                using var destinationStream = copied.Open();
                sourceStream.CopyTo(destinationStream);
            }
        }

        using (var archive = ZipFile.Open(tamperedBackup, ZipArchiveMode.Update))
        {
            var firstFile = result.Manifest.Files[0];
            var tamperedEntry = archive.GetEntry(firstFile.RelativePath) ?? throw new InvalidOperationException("Target entry missing.");
            tamperedEntry.Delete();
            var overwritten = archive.CreateEntry(firstFile.RelativePath, CompressionLevel.Optimal);
            using var overwriteStream = overwritten.Open();
            overwriteStream.Write(Encoding.UTF8.GetBytes("tampered"), 0, 8);
        }

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceBackupService.Restore(tamperedBackup, Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-tamper-restore-{Guid.NewGuid():N}")));

        if (File.Exists(sourceBackup))
        {
            File.Delete(sourceBackup);
        }

        if (File.Exists(tamperedBackup))
        {
            File.Delete(tamperedBackup);
        }
    }

    [TestMethod]
    public void Restore_rejects_traversal_entry()
    {
        using var workspace = TemporaryWorkspace.Create();
        WriteTextFile(workspace.Root, "safe.txt", "ok");
        ResearchWorkspaceStore.WriteProject(workspace.Location, workspace.Project);

        var sourceBackup = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-traversal-{Guid.NewGuid():N}.zip");
        var malicious = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-traversal-target-{Guid.NewGuid():N}.zip");
        ResearchWorkspaceBackupService.Create(workspace.Root, sourceBackup, BackupTime);

        using (var source = ZipFile.OpenRead(sourceBackup))
        using (var target = ZipFile.Open(malicious, ZipArchiveMode.Create))
        {
            foreach (var entry in source.Entries)
            {
                var copied = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var sourceStream = entry.Open();
                using var destinationStream = copied.Open();
                sourceStream.CopyTo(destinationStream);
            }

            var traversal = target.CreateEntry("../outside.txt", CompressionLevel.Optimal);
            using var traversalStream = traversal.Open();
            var bytes = Encoding.UTF8.GetBytes("escape");
            traversalStream.Write(bytes, 0, bytes.Length);
        }

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceBackupService.Restore(malicious, Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-traversal-restore-{Guid.NewGuid():N}")));

        if (File.Exists(sourceBackup))
        {
            File.Delete(sourceBackup);
        }

        if (File.Exists(malicious))
        {
            File.Delete(malicious);
        }
    }

    [TestMethod]
    public void Restore_rejects_manifest_duplicate_paths()
    {
        using var workspace = TemporaryWorkspace.Create();
        WriteTextFile(workspace.Root, "dup.txt", "value");
        ResearchWorkspaceStore.WriteProject(workspace.Location, workspace.Project);

        var sourceBackup = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-dup-{Guid.NewGuid():N}.zip");
        var malformed = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-dup-target-{Guid.NewGuid():N}.zip");
        ResearchWorkspaceBackupService.Create(workspace.Root, sourceBackup, BackupTime);
        using (var sourceArchive = ZipFile.OpenRead(sourceBackup))
        {
            var originalManifest = JsonSerializer.Deserialize<ResearchWorkspaceBackupManifest>(
                ReadEntryBytes(sourceArchive.GetEntry(ResearchWorkspaceBackupService.BackupManifestFileName)))!;

            var duplicateManifest = originalManifest with
            {
                Files = originalManifest.Files.Append(originalManifest.Files[0]).ToArray(),
                FileCount = originalManifest.FileCount + 1,
                TotalBytes = originalManifest.TotalBytes + originalManifest.Files[0].SizeBytes,
                ManifestDigest = string.Empty
            };
            duplicateManifest = duplicateManifest with
            {
                ManifestDigest = ComputeManifestDigest(duplicateManifest)
            };

            using (var destination = ZipFile.Open(malformed, ZipArchiveMode.Create))
            {
                foreach (var entry in sourceArchive.Entries)
                {
                    if (string.Equals(
                        entry.FullName,
                        ResearchWorkspaceBackupService.BackupManifestFileName,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var copy = destination.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var sourceStream = entry.Open();
                    using var destinationStream = copy.Open();
                    sourceStream.CopyTo(destinationStream);
                }

                var replacedManifest = destination.CreateEntry(
                    ResearchWorkspaceBackupService.BackupManifestFileName,
                    CompressionLevel.Optimal);
                using var manifestStream = replacedManifest.Open();
                var bytes = SerializeManifest(duplicateManifest);
                manifestStream.Write(bytes, 0, bytes.Length);
            }
        }

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceBackupService.Restore(malformed, Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-dup-restore-{Guid.NewGuid():N}")));

        if (File.Exists(sourceBackup))
        {
            File.Delete(sourceBackup);
        }

        if (File.Exists(malformed))
        {
            File.Delete(malformed);
        }
    }

    [TestMethod]
    public void Restore_rejects_existing_target_directory()
    {
        using var workspace = TemporaryWorkspace.Create();
        WriteTextFile(workspace.Root, "existing.txt", "before");
        ResearchWorkspaceStore.WriteProject(workspace.Location, workspace.Project);

        var sourceBackup = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-existing-{Guid.NewGuid():N}.zip");
        ResearchWorkspaceBackupService.Create(workspace.Root, sourceBackup, BackupTime);
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-existing-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(restoreRoot);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceBackupService.Restore(sourceBackup, restoreRoot));

        if (File.Exists(sourceBackup))
        {
            File.Delete(sourceBackup);
        }

        if (Directory.Exists(restoreRoot))
        {
            Directory.Delete(restoreRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Restore_rejects_target_inside_existing_workspace()
    {
        using var workspace = TemporaryWorkspace.Create();
        var backupPath = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-nested-target-{Guid.NewGuid():N}.zip");
        var target = Path.Combine(workspace.Root, "restored-copy");
        try
        {
            ResearchWorkspaceBackupService.Create(workspace.Root, backupPath, BackupTime);

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceBackupService.Restore(backupPath, target));

            StringAssert.Contains(exception.Message, "inside an existing Nexus workspace");
            Assert.IsFalse(Directory.Exists(target));
            Assert.IsFalse(Directory.Exists(target + ".restore-staging"));
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    [TestMethod]
    public void Restore_failure_after_extraction_removes_staging_and_never_promotes_target()
    {
        using var workspace = TemporaryWorkspace.Create();
        const string relativeInput = "inputs/search/restore-failure.csv";
        const string relativeTrace = "nexus-output/imports/restore-failure.import-trace.json";
        var inputPath = Path.Combine(workspace.Root, relativeInput);
        WriteTextFile(workspace.Root, relativeInput, "id,title\n1,original");
        WriteTextFile(workspace.Root, relativeTrace, "{}");
        var project = workspace.Project.WithInput(new ResearchWorkspaceInput
        {
            InputId = "restore-failure",
            Kind = "search-export",
            Source = "scopus",
            Format = "csv",
            RelativePath = relativeInput,
            Sha256 = Sha256(File.ReadAllBytes(inputPath)),
            QueryId = "restore-failure",
            QueryText = "restore failure",
            ImportTracePath = relativeTrace
        });
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);

        var sourceBackup = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-post-extract-{Guid.NewGuid():N}.zip");
        var malformedBackup = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-post-extract-invalid-{Guid.NewGuid():N}.zip");
        var target = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-post-extract-target-{Guid.NewGuid():N}");
        var changedBytes = Encoding.UTF8.GetBytes("id,title\n1,changed-after-manifest");

        try
        {
            ResearchWorkspaceBackupService.Create(workspace.Root, sourceBackup, BackupTime);
            using var source = ZipFile.OpenRead(sourceBackup);
            var originalManifest = JsonSerializer.Deserialize<ResearchWorkspaceBackupManifest>(
                ReadEntryBytes(source.GetEntry(ResearchWorkspaceBackupService.BackupManifestFileName)))!;
            var files = originalManifest.Files
                .Select(file => file.RelativePath == relativeInput
                    ? file with
                    {
                        SizeBytes = changedBytes.Length,
                        Sha256 = Sha256(changedBytes)
                    }
                    : file)
                .ToArray();
            var invalidManifest = originalManifest with
            {
                Files = files,
                TotalBytes = files.Sum(file => file.SizeBytes),
                ManifestDigest = string.Empty
            };
            invalidManifest = invalidManifest with
            {
                ManifestDigest = ComputeManifestDigest(invalidManifest)
            };

            using (var destination = ZipFile.Open(malformedBackup, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    if (entry.FullName == ResearchWorkspaceBackupService.BackupManifestFileName)
                    {
                        continue;
                    }

                    var copy = destination.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var output = copy.Open();
                    if (entry.FullName == relativeInput)
                    {
                        output.Write(changedBytes);
                    }
                    else
                    {
                        using var input = entry.Open();
                        input.CopyTo(output);
                    }
                }

                var manifestEntry = destination.CreateEntry(
                    ResearchWorkspaceBackupService.BackupManifestFileName,
                    CompressionLevel.Optimal);
                using var manifestOutput = manifestEntry.Open();
                manifestOutput.Write(SerializeManifest(invalidManifest));
            }

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceBackupService.Restore(malformedBackup, target));
            Assert.IsFalse(Directory.Exists(target), "A failed restore promoted a target workspace.");
            Assert.IsFalse(
                Directory.Exists(target + ".restore-staging"),
                "A failed restore left staging material.");
        }
        finally
        {
            foreach (var path in new[] { sourceBackup, malformedBackup })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
            if (Directory.Exists(target + ".restore-staging"))
            {
                Directory.Delete(target + ".restore-staging", recursive: true);
            }
        }
    }

    [TestMethod]
    public void Restore_injected_failure_after_file_write_removes_staging_and_target()
    {
        AssertInjectedRestoreFailureCleansUp(ResearchWorkspaceRestorePoint.AfterFileWritten);
    }

    [TestMethod]
    public void Restore_injected_failure_before_promotion_removes_staging_and_target()
    {
        AssertInjectedRestoreFailureCleansUp(ResearchWorkspaceRestorePoint.BeforePromotion);
    }

    [TestMethod]
    public void Restore_promotion_collision_removes_staging_without_overwriting_external_target()
    {
        using var workspace = TemporaryWorkspace.Create();
        var backupPath = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-promotion-collision-{Guid.NewGuid():N}.zip");
        var target = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-promotion-collision-target-{Guid.NewGuid():N}");
        var markerPath = Path.Combine(target, "external-owner.txt");

        try
        {
            ResearchWorkspaceBackupService.Create(workspace.Root, backupPath, BackupTime);

            Assert.ThrowsExactly<IOException>(() =>
                ResearchWorkspaceBackupService.RestoreCore(
                    backupPath,
                    target,
                    context =>
                    {
                        if (context.Point == ResearchWorkspaceRestorePoint.BeforePromotion)
                        {
                            Directory.CreateDirectory(target);
                            File.WriteAllText(markerPath, "external target", Encoding.UTF8);
                        }
                    }));

            Assert.IsTrue(File.Exists(markerPath), "Restore removed or overwrote an externally created target.");
            Assert.IsFalse(
                File.Exists(Path.Combine(target, ResearchWorkspacePaths.ProjectFileName)),
                "Restore merged workspace files into an externally created target.");
            Assert.IsFalse(
                Directory.Exists(target + ".restore-staging"),
                "A failed directory promotion left staging material.");
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
            if (Directory.Exists(target + ".restore-staging"))
            {
                Directory.Delete(target + ".restore-staging", recursive: true);
            }
        }
    }

    [TestMethod]
    public void Create_rejects_held_workspace_lock()
    {
        using var workspace = TemporaryWorkspace.Create();
        var lockPath = Path.Combine(workspace.Root, ResearchWorkspacePaths.ProjectLockFileName);
        using var lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() =>
            ResearchWorkspaceBackupService.Create(
                workspace.Root,
                Path.Combine(Path.GetTempPath(), $"nexus-rw-lock-{Guid.NewGuid():N}.zip"),
                BackupTime));
    }

    [TestMethod]
    public void Create_rejects_destination_inside_workspace()
    {
        using var workspace = TemporaryWorkspace.Create();
        var destination = Path.Combine(workspace.Root, "unsafe-backup.zip");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceBackupService.Create(workspace.Root, destination, BackupTime));

        Assert.IsFalse(File.Exists(destination));
    }

    [TestMethod]
    public void Create_fails_closed_when_source_changes_during_capture()
    {
        using var workspace = TemporaryWorkspace.Create();
        var source = Path.Combine(workspace.Root, "source.txt");
        File.WriteAllText(source, "before", Encoding.UTF8);
        var destination = Path.Combine(Path.GetTempPath(), $"nexus-rw-race-{Guid.NewGuid():N}.zip");

        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceBackupService.CreateCore(
                    workspace.Root,
                    destination,
                    BackupTime,
                    context =>
                    {
                        if (context.Point == ResearchWorkspaceBackupCapturePoint.BeforeCompletionVerification)
                        {
                            File.AppendAllText(source, "-changed", Encoding.UTF8);
                        }
                    }));

            Assert.IsFalse(File.Exists(destination));
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    [TestMethod]
    public void Create_with_fixed_time_has_deterministic_manifest_identity()
    {
        using var workspace = TemporaryWorkspace.Create();
        var first = Path.Combine(Path.GetTempPath(), $"nexus-rw-deterministic-a-{Guid.NewGuid():N}.zip");
        var second = Path.Combine(Path.GetTempPath(), $"nexus-rw-deterministic-b-{Guid.NewGuid():N}.zip");
        try
        {
            var firstResult = ResearchWorkspaceBackupService.Create(workspace.Root, first, BackupTime);
            var secondResult = ResearchWorkspaceBackupService.Create(workspace.Root, second, BackupTime);

            Assert.AreEqual(firstResult.ManifestDigest, secondResult.ManifestDigest);
            CollectionAssert.AreEqual(
                firstResult.Manifest.Files.ToArray(),
                secondResult.Manifest.Files.ToArray());
            Assert.AreEqual(
                Sha256(File.ReadAllBytes(first)),
                Sha256(File.ReadAllBytes(second)),
                "Fixed-time backups of unchanged workspace bytes must be byte-identical.");
        }
        finally
        {
            if (File.Exists(first)) File.Delete(first);
            if (File.Exists(second)) File.Delete(second);
        }
    }

    [TestMethod]
    public void Create_rejects_reparse_point_paths_where_supported()
    {
        using var workspace = TemporaryWorkspace.Create();
        var externalRoot = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-external-{Guid.NewGuid():N}");
        var linkedPath = Path.Combine(workspace.Root, "linked");
        Directory.CreateDirectory(externalRoot);
        try
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkedPath}\" \"{externalRoot}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    process!.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException("Unable to create junction.");
                    }
                }
                else
                {
                    Directory.CreateSymbolicLink(linkedPath, externalRoot);
                }
            }
            catch (Exception exception) when (exception is PlatformNotSupportedException or UnauthorizedAccessException or NotSupportedException)
            {
                Assert.Inconclusive("Creating links is not supported in this environment.");
            }
            catch (NotImplementedException)
            {
                Assert.Inconclusive("Creating links is not supported in this environment.");
            }

            WriteTextFile(externalRoot, "escape.txt", "outside");
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceBackupService.Create(
                    workspace.Root,
                    Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-link-{Guid.NewGuid():N}.zip"),
                    BackupTime));
        }
        finally
        {
            if (Directory.Exists(linkedPath))
            {
                Directory.Delete(linkedPath, recursive: true);
            }

            if (Directory.Exists(externalRoot))
            {
                Directory.Delete(externalRoot, recursive: true);
            }
        }
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void AssertInjectedRestoreFailureCleansUp(ResearchWorkspaceRestorePoint failurePoint)
    {
        using var workspace = TemporaryWorkspace.Create();
        var backupPath = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-restore-fault-{failurePoint}-{Guid.NewGuid():N}.zip");
        var target = Path.Combine(
            Path.GetTempPath(),
            $"nexus-rw-backup-restore-fault-target-{Guid.NewGuid():N}");

        try
        {
            ResearchWorkspaceBackupService.Create(workspace.Root, backupPath, BackupTime);

            Assert.ThrowsExactly<IOException>(() =>
                ResearchWorkspaceBackupService.RestoreCore(
                    backupPath,
                    target,
                    context =>
                    {
                        if (context.Point == failurePoint)
                        {
                            throw new IOException($"Injected restore failure at {failurePoint}.");
                        }
                    }));

            Assert.IsFalse(Directory.Exists(target), "A failed restore promoted a target workspace.");
            Assert.IsFalse(
                Directory.Exists(target + ".restore-staging"),
                "A failed restore left staging material.");
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
            if (Directory.Exists(target + ".restore-staging"))
            {
                Directory.Delete(target + ".restore-staging", recursive: true);
            }
        }
    }

    private static IEnumerable<string> SnapshotRegularFiles(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string ComputeManifestDigest(ResearchWorkspaceBackupManifest manifest)
    {
        var bytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(
            new CanonicalJsonObject()
                .Add("schema", ResearchWorkspaceBackupService.BackupManifestSchema)
                .Add("schema_version", ResearchWorkspaceBackupService.BackupManifestVersion)
                .Add("workspace_id", manifest.WorkspaceId)
                .Add("project_revision", manifest.ProjectRevision)
                .Add("workspace_created_at", manifest.WorkspaceCreatedAtUtc)
                .Add("archive_created_at", manifest.ArchiveCreatedAtUtc)
                .Add("total_bytes", manifest.TotalBytes)
                .Add("file_count", manifest.FileCount)
                .Add("files", CanonicalJsonValue.Array(manifest.Files
                    .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => (CanonicalJsonValue)new CanonicalJsonObject()
                        .Add("path", file.RelativePath)
                        .Add("size", file.SizeBytes)
                        .Add("sha256", file.Sha256))
                    .ToArray())));
        return ContentDigest.Sha256(bytes).ToString();
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

    private static string Sha256(byte[] bytes)
    {
        return ContentDigest.Sha256(bytes).ToString();
    }

    private static void WriteTextFile(string root, string relativePath, string text)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, Encoding.UTF8);
    }

    private static string ReadProjectFile(string root) =>
        ResearchWorkspacePaths.ProjectFile(root);

    private static readonly DateTimeOffset BackupTime =
        new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root)
        {
            Root = root;
            Project = ResearchWorkspaceProject.Create(
                "APP-01 backup service test",
                new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
            foreach (var relativeDirectory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(Root, relativeDirectory));
            }

            var projectFile = ReadProjectFile(Root);
            ResearchWorkspaceJson.WriteProjectFile(projectFile, Project);
            Location = new ResearchWorkspaceLocation(Root, projectFile);
        }

        public string Root { get; }
        public ResearchWorkspaceProject Project { get; }
        public ResearchWorkspaceLocation Location { get; }

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-rw-backup-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TemporaryWorkspace(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
