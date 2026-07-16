using System.Text.Json;
using NexusScholar.AppServices;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Screening;
using NexusScholar.Screening.FullText;

namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceFullTextArtifact(string Name, string RelativePath, string Sha256);

public sealed record ResearchWorkspaceFullTextManifest(
    string Schema,
    string GenerationId,
    string WorkspaceId,
    long ProjectRevision,
    string CandidateId,
    string AdmissionDigest,
    string InputDigest,
    string AcquisitionDigest,
    string ArtifactDigest,
    string RawArtifactDigest,
    string? ExtractionAttemptDigest,
    string? PredecessorGenerationId,
    string? PredecessorManifestSha256,
    IReadOnlyList<ResearchWorkspaceFullTextArtifact> Artifacts)
{
    public const string CurrentSchema = "nexus.workspace-fulltext-generation.v1";
}

public sealed record ResearchWorkspaceFullTextRecord(string Name, byte[] CanonicalBytes)
{
    public ContentDigest Digest => ContentDigest.Sha256(CanonicalBytes);
}

public sealed record VerifiedResearchWorkspaceFullTextGeneration(
    ResearchWorkspaceFullTextManifest Manifest,
    VerifiedFullTextAdmission Admission,
    VerifiedFullTextChain Authority,
    FullTextExtractionAttempt? ExtractionAttempt,
    IReadOnlyDictionary<string, byte[]> AdditionalRecords,
    FullTextScreeningConductJournal? ConductJournal,
    FullTextScreeningConductHandoff? ConductHandoff);

public sealed record ResearchWorkspaceFullTextCommit(
    ResearchWorkspaceProject Project,
    ResearchWorkspaceFullTextManifest Manifest,
    bool AlreadyApplied);

public static class ResearchWorkspaceFullTextManifestCodec
{
    public static byte[] Serialize(ResearchWorkspaceFullTextManifest value)
    {
        Validate(value);
        var root = new CanonicalJsonObject()
            .Add("schema", value.Schema)
            .Add("generation_id", value.GenerationId)
            .Add("workspace_id", value.WorkspaceId)
            .Add("project_revision", value.ProjectRevision)
            .Add("candidate_id", value.CandidateId)
            .Add("admission_digest", value.AdmissionDigest)
            .Add("input_digest", value.InputDigest)
            .Add("acquisition_digest", value.AcquisitionDigest)
            .Add("artifact_digest", value.ArtifactDigest)
            .Add("raw_artifact_digest", value.RawArtifactDigest)
            .Add("artifacts", CanonicalJsonValue.Array(value.Artifacts.OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => new CanonicalJsonObject().Add("name", item.Name).Add("relative_path", item.RelativePath).Add("sha256", item.Sha256)).ToArray()));
        if (value.ExtractionAttemptDigest is not null) root.Add("extraction_attempt_digest", value.ExtractionAttemptDigest);
        if (value.PredecessorGenerationId is not null)
            root.Add("predecessor_generation_id", value.PredecessorGenerationId).Add("predecessor_manifest_sha256", value.PredecessorManifestSha256!);
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(root);
    }

    public static ResearchWorkspaceFullTextManifest Rehydrate(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        if (CanonicalJsonValue.FromJsonElement(document.RootElement) is not CanonicalJsonObject root ||
            !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
            throw new InvalidOperationException("Full Text workspace manifest must use canonical JSON bytes.");
        var required = new[] { "schema", "generation_id", "workspace_id", "project_revision", "candidate_id", "admission_digest", "input_digest", "acquisition_digest", "artifact_digest", "raw_artifact_digest", "artifacts" };
        var allowed = required.Concat(new[] { "extraction_attempt_digest", "predecessor_generation_id", "predecessor_manifest_sha256" }).ToHashSet(StringComparer.Ordinal);
        if (!required.All(root.Properties.ContainsKey) || root.Properties.Keys.Any(key => !allowed.Contains(key)))
            throw new InvalidOperationException("Full Text workspace manifest has missing or unknown fields.");
        var manifest = new ResearchWorkspaceFullTextManifest(
            Text(root, "schema"), Text(root, "generation_id"), Text(root, "workspace_id"), Number(root, "project_revision"), Text(root, "candidate_id"),
            Text(root, "admission_digest"), Text(root, "input_digest"), Text(root, "acquisition_digest"), Text(root, "artifact_digest"), Text(root, "raw_artifact_digest"),
            Optional(root, "extraction_attempt_digest"), Optional(root, "predecessor_generation_id"), Optional(root, "predecessor_manifest_sha256"),
            Array(root, "artifacts").Select(ParseArtifact).ToArray());
        Validate(manifest);
        if (!bytes.SequenceEqual(Serialize(manifest))) throw new InvalidOperationException("Full Text workspace manifest is not reproducible.");
        return manifest;
    }

    private static void Validate(ResearchWorkspaceFullTextManifest value)
    {
        if (value.Schema != ResearchWorkspaceFullTextManifest.CurrentSchema || value.ProjectRevision <= 0 || value.Artifacts.Count < 5 ||
            value.Artifacts.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != value.Artifacts.Count ||
            value.Artifacts.Select(item => item.RelativePath).Distinct(StringComparer.Ordinal).Count() != value.Artifacts.Count ||
            (value.PredecessorGenerationId is null) != (value.PredecessorManifestSha256 is null))
            throw new InvalidOperationException("Full Text workspace manifest shape is invalid.");
        foreach (var digest in new[] { value.AdmissionDigest, value.InputDigest, value.AcquisitionDigest, value.ArtifactDigest, value.RawArtifactDigest }
            .Concat(value.ExtractionAttemptDigest is null ? [] : [value.ExtractionAttemptDigest])
            .Concat(value.PredecessorManifestSha256 is null ? [] : [value.PredecessorManifestSha256])
            .Concat(value.Artifacts.Select(item => item.Sha256))) _ = ContentDigest.Parse(digest);

        var artifacts = value.Artifacts.ToDictionary(item => item.Name, StringComparer.Ordinal);
        RequireArtifactDigest(artifacts, "admission", value.AdmissionDigest);
        RequireArtifactDigest(artifacts, "input", value.InputDigest);
        RequireArtifactDigest(artifacts, "acquisition", value.AcquisitionDigest);
        RequireArtifactDigest(artifacts, "artifact-evidence", value.ArtifactDigest);
        RequireArtifactDigest(artifacts, "raw-artifact", value.RawArtifactDigest);
        if (value.ExtractionAttemptDigest is null)
        {
            if (artifacts.ContainsKey("extraction-attempt"))
                throw new InvalidOperationException("Full Text manifest cannot inventory an extraction attempt without its authority digest.");
        }
        else
        {
            RequireArtifactDigest(artifacts, "extraction-attempt", value.ExtractionAttemptDigest);
        }
    }

    private static void RequireArtifactDigest(
        IReadOnlyDictionary<string, ResearchWorkspaceFullTextArtifact> artifacts,
        string name,
        string digest)
    {
        if (!artifacts.TryGetValue(name, out var artifact) || !string.Equals(artifact.Sha256, digest, StringComparison.Ordinal))
            throw new InvalidOperationException($"Full Text manifest artifact '{name}' is missing or does not match its authority digest.");
    }

    private static ResearchWorkspaceFullTextArtifact ParseArtifact(CanonicalJsonValue value)
    {
        if (value is not CanonicalJsonObject item || item.Properties.Count != 3) throw new InvalidOperationException("Full Text artifact inventory entry is invalid.");
        return new ResearchWorkspaceFullTextArtifact(Text(item, "name"), Text(item, "relative_path"), Text(item, "sha256"));
    }
    private static string Text(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonString text ? text.Value : throw new InvalidOperationException($"Full Text manifest field '{name}' must be text.");
    private static string? Optional(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Text(root, name) : null;
    private static long Number(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonNumber number && long.TryParse(number.Value, out var result) ? result : throw new InvalidOperationException($"Full Text manifest field '{name}' must be an integer.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonArray array ? array.Items : throw new InvalidOperationException($"Full Text manifest field '{name}' must be an array.");
}

public static class ResearchWorkspaceFullTextGenerationVerifier
{
    public static ResearchWorkspaceFullTextManifest VerifyCurrentIntegrity(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project) => VerifyCurrentFiles(location, project).Manifest;

    public static VerifiedResearchWorkspaceFullTextGeneration VerifyCurrent(
        ResearchWorkspaceLocation location, ResearchWorkspaceProject project, ScreeningConductJournal journal,
        ScreeningConductHandoff handoff, long maximumBytes,
        FullTextScreeningConductPolicy? expectedConductPolicy = null)
    {
        var verifiedFiles = VerifyCurrentFiles(location, project);
        var manifest = verifiedFiles.Manifest;
        var bytes = verifiedFiles.Bytes;
        var admission = VerifiedFullTextAdmissionCanonicalCodec.Rehydrate(bytes["admission"], ContentDigest.Parse(manifest.AdmissionDigest), journal, handoff);
        var authority = FullTextAuthorityCanonicalCodec.Rehydrate(bytes["input"], ContentDigest.Parse(manifest.InputDigest), bytes["acquisition"], ContentDigest.Parse(manifest.AcquisitionDigest),
            bytes["artifact-evidence"], ContentDigest.Parse(manifest.ArtifactDigest), bytes["raw-artifact"], maximumBytes);
        if (admission.InputDigest != ContentDigest.Parse(manifest.InputDigest) || authority.Input.CandidateId != admission.CandidateId || authority.Artifact.RawByteDigest != manifest.RawArtifactDigest)
            throw new InvalidOperationException("Full Text workspace authority bindings do not agree.");
        FullTextExtractionAttempt? extraction = null;
        if (manifest.ExtractionAttemptDigest is not null)
            extraction = FullTextExtractionAttemptCodec.Rehydrate(bytes["extraction-attempt"], ContentDigest.Parse(manifest.ExtractionAttemptDigest), authority);
        var reserved = new HashSet<string>(["admission", "input", "acquisition", "artifact-evidence", "raw-artifact", "extraction-attempt"], StringComparer.Ordinal);
        var additional = bytes.Where(pair => !reserved.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        FullTextScreeningConductJournal? conductJournal = null;
        FullTextScreeningConductHandoff? conductHandoff = null;
        if (additional.Count > 0)
        {
            if (expectedConductPolicy is null || !additional.ContainsKey("conduct-policy") || !additional.ContainsKey("conduct-header"))
                throw new InvalidOperationException("Full Text conduct records require the exact verified conduct policy and header.");
            var policyArtifact = manifest.Artifacts.Single(item => item.Name == "conduct-policy");
            var policy = FullTextScreeningConductCanonicalCodec.RehydratePolicy(
                additional["conduct-policy"], ContentDigest.Parse(policyArtifact.Sha256), expectedConductPolicy);
            var headerArtifact = manifest.Artifacts.Single(item => item.Name == "conduct-header");
            var header = FullTextScreeningConductCanonicalCodec.RehydrateHeader(
                additional["conduct-header"], ContentDigest.Parse(headerArtifact.Sha256), policy);
            var entries = new List<IFullTextScreeningConductEntry>();
            var entryArtifacts = manifest.Artifacts.Where(item => item.Name.StartsWith("conduct-entry-", StringComparison.Ordinal))
                .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            for (var index = 0; index < entryArtifacts.Length; index++)
            {
                if (entryArtifacts[index].Name != $"conduct-entry-{index + 1:D6}")
                    throw new InvalidOperationException("Full Text conduct entry names must be contiguous and ordered.");
                var payload = additional[entryArtifacts[index].Name];
                using var entryDocument = JsonDocument.Parse(payload);
                var schema = entryDocument.RootElement.GetProperty("schema").GetString();
                var digest = ContentDigest.Parse(entryArtifacts[index].Sha256);
                entries.Add(schema switch
                {
                    FullTextScreeningConductSchema.DecisionSchemaId => FullTextScreeningConductCanonicalCodec.RehydrateDecision(payload, digest, header, extraction),
                    FullTextScreeningConductSchema.InvalidationSchemaId => FullTextScreeningConductCanonicalCodec.RehydrateInvalidation(payload, digest, header),
                    _ => throw new InvalidOperationException("Unknown Full Text conduct entry schema.")
                });
            }
            conductJournal = FullTextScreeningConductJournal.RehydrateEntries(header, policy, entries);
            if (additional.TryGetValue("conduct-handoff", out var conductHandoffBytes))
            {
                var handoffArtifact = manifest.Artifacts.Single(item => item.Name == "conduct-handoff");
                conductHandoff = FullTextScreeningConductCanonicalCodec.RehydrateHandoff(
                    conductHandoffBytes, ContentDigest.Parse(handoffArtifact.Sha256), conductJournal);
            }
            var allowedConduct = entryArtifacts.Select(item => item.Name).Append("conduct-policy").Append("conduct-header")
                .Concat(conductHandoff is null ? [] : ["conduct-handoff"]).ToHashSet(StringComparer.Ordinal);
            if (additional.Keys.Any(key => !allowedConduct.Contains(key)))
                throw new InvalidOperationException("Full Text generation contains an unknown conduct record.");
        }
        return new VerifiedResearchWorkspaceFullTextGeneration(manifest, admission, authority, extraction,
            additional, conductJournal, conductHandoff);
    }

    private static (ResearchWorkspaceFullTextManifest Manifest, IReadOnlyDictionary<string, byte[]> Bytes) VerifyCurrentFiles(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        if (project.CurrentFullTextGenerationId is null || project.FullTextManifestPath is null || project.FullTextManifestSha256 is null)
            throw new InvalidOperationException("The workspace has no current Full Text generation.");
        var manifestPath = Resolve(location, project.FullTextManifestPath);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        if (ContentDigest.Sha256(manifestBytes).ToString() != project.FullTextManifestSha256)
            throw new InvalidOperationException("Full Text manifest failed pointer digest verification.");
        var manifest = ResearchWorkspaceFullTextManifestCodec.Rehydrate(manifestBytes);
        if (manifest.GenerationId != project.CurrentFullTextGenerationId || manifest.WorkspaceId != project.WorkspaceId || manifest.ProjectRevision != project.Revision)
            throw new InvalidOperationException("Full Text manifest is stale or bound to another workspace.");
        var bytes = manifest.Artifacts.ToDictionary(item => item.Name, item =>
        {
            var payload = File.ReadAllBytes(Resolve(location, item.RelativePath));
            if (ContentDigest.Sha256(payload).ToString() != item.Sha256)
                throw new InvalidOperationException($"Full Text artifact '{item.Name}' failed digest verification.");
            return payload;
        }, StringComparer.Ordinal);
        var root = Path.GetDirectoryName(manifestPath)!;
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var expectedFiles = manifest.Artifacts.Select(item => Path.GetFullPath(Resolve(location, item.RelativePath)))
            .Append(Path.GetFullPath(manifestPath)).ToHashSet(pathComparer);
        if (Directory.GetFiles(root, "*", SearchOption.AllDirectories).Any(path => !expectedFiles.Contains(Path.GetFullPath(path))))
            throw new InvalidOperationException("Full Text generation contains an unmanifested file.");
        return (manifest, bytes);
    }

    private static string Resolve(ResearchWorkspaceLocation location, string relative)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relative, out var path) || !File.Exists(path))
            throw new InvalidOperationException("Full Text generation file is missing or outside the workspace.");
        return path;
    }
}

public static class ResearchWorkspaceFullTextTransaction
{
    public static ResearchWorkspaceFullTextCommit Commit(
        ResearchWorkspaceLocation location, ResearchWorkspaceProject expectedProject, ScreeningConductJournal journal,
        ScreeningConductHandoff handoff, VerifiedFullTextAdmission admission, VerifiedFullTextChain authority,
        byte[] rawBytes, long maximumBytes, FullTextExtractionAttempt? extractionAttempt = null,
        IReadOnlyList<ResearchWorkspaceFullTextRecord>? additionalRecords = null,
        FullTextScreeningConductPolicy? conductPolicy = null,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);
        var admissionBytes = VerifiedFullTextAdmissionCanonicalCodec.Serialize(admission);
        _ = VerifiedFullTextAdmissionCanonicalCodec.Rehydrate(admissionBytes, admission.Digest, journal, handoff);
        var inputBytes = FullTextAuthorityCanonicalCodec.Serialize(authority.Input);
        var acquisitionBytes = FullTextAuthorityCanonicalCodec.Serialize(authority.Acquisition);
        var artifactBytes = FullTextAuthorityCanonicalCodec.Serialize(authority.Artifact);
        _ = FullTextAuthorityCanonicalCodec.Rehydrate(inputBytes, ContentDigest.Sha256(inputBytes), acquisitionBytes, ContentDigest.Sha256(acquisitionBytes), artifactBytes, ContentDigest.Sha256(artifactBytes), rawBytes, maximumBytes);
        if (admission.InputDigest != ContentDigest.Sha256(inputBytes) || admission.CandidateId != authority.Input.CandidateId)
            throw new InvalidOperationException("Full Text admission and artifact authority do not bind the same input.");
        byte[]? extractionBytes = null;
        if (extractionAttempt is not null)
        {
            extractionBytes = FullTextExtractionAttemptCodec.Serialize(extractionAttempt);
            _ = FullTextExtractionAttemptCodec.Rehydrate(extractionBytes, extractionAttempt.Digest, authority);
        }
        var records = new List<(string Name, string FileName, byte[] Bytes)>
        {
            ("admission", "admission.json", admissionBytes), ("input", "input.json", inputBytes),
            ("acquisition", "acquisition.json", acquisitionBytes), ("artifact-evidence", "artifact-evidence.json", artifactBytes),
            ("raw-artifact", "raw-artifact.bin", rawBytes)
        };
        if (extractionBytes is not null) records.Add(("extraction-attempt", "extraction-attempt.json", extractionBytes));
        foreach (var record in additionalRecords ?? [])
        {
            if (string.IsNullOrWhiteSpace(record.Name) || records.Any(item => item.Name == record.Name) || record.Name.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_'))
                throw new InvalidOperationException("Additional Full Text record name is invalid or duplicated.");
            records.Add((record.Name, $"{record.Name}.json", record.CanonicalBytes));
        }
        var stateDigest = ContentDigest.Sha256Utf8(string.Join("|", records.OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => $"{item.Name}:{ContentDigest.Sha256(item.Bytes)}")));
        var generationId = $"fulltext-{stateDigest.Value[7..23]}";
        if (expectedProject.CurrentFullTextGenerationId == generationId)
        {
            var current = ResearchWorkspaceFullTextGenerationVerifier.VerifyCurrent(location, expectedProject, journal, handoff, maximumBytes, conductPolicy);
            return new ResearchWorkspaceFullTextCommit(expectedProject, current.Manifest, true);
        }
        var relativeRoot = ResearchWorkspacePaths.FullTextGenerationRoot(admission.CandidateId, generationId);
        var finalRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, relativeRoot);
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, $"{ResearchWorkspacePaths.GenerationStaging}/{generationId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        try
        {
            var artifacts = records.Select(item =>
            {
                File.WriteAllBytes(Path.Combine(stagingRoot, item.FileName), item.Bytes);
                return new ResearchWorkspaceFullTextArtifact(item.Name, $"{relativeRoot}/{item.FileName}", ContentDigest.Sha256(item.Bytes).ToString());
            }).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            var manifestPath = $"{relativeRoot}/fulltext.manifest.json";
            var placeholder = "sha256:" + new string('0', 64);
            var committed = expectedProject.CommitFullTextGeneration(generationId, manifestPath, placeholder);
            var manifest = new ResearchWorkspaceFullTextManifest(ResearchWorkspaceFullTextManifest.CurrentSchema, generationId, expectedProject.WorkspaceId, committed.Revision,
                admission.CandidateId, admission.Digest.ToString(), ContentDigest.Sha256(inputBytes).ToString(), ContentDigest.Sha256(acquisitionBytes).ToString(), ContentDigest.Sha256(artifactBytes).ToString(),
                authority.Artifact.RawByteDigest, extractionAttempt?.Digest.ToString(), expectedProject.CurrentFullTextGenerationId, expectedProject.FullTextManifestSha256, artifacts);
            var manifestBytes = ResearchWorkspaceFullTextManifestCodec.Serialize(manifest);
            committed = committed with { FullTextManifestSha256 = ContentDigest.Sha256(manifestBytes).ToString() };
            File.WriteAllBytes(Path.Combine(stagingRoot, "fulltext.manifest.json"), manifestBytes);
            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterStaging);
            Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
            using var workspaceLock = new FileStream(Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (currentProject.Revision != expectedProject.Revision || currentProject.CurrentFullTextGenerationId != expectedProject.CurrentFullTextGenerationId || currentProject.FullTextManifestSha256 != expectedProject.FullTextManifestSha256)
                throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, currentProject.Revision);
            try
            {
                if (Directory.Exists(finalRoot)) throw new ResearchWorkspaceConcurrencyException("Full Text generation identity collision.", new InvalidOperationException());
                Directory.Move(stagingRoot, finalRoot);
                faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterPromotion);
                ResearchWorkspaceStore.WriteProject(location, committed);
            }
            catch
            {
                if (Directory.Exists(finalRoot))
                {
                    var quarantine = ResearchWorkspacePaths.InProject(location.RootDirectory, $"{ResearchWorkspacePaths.GenerationQuarantine}/{generationId}-{Guid.NewGuid():N}");
                    Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
                    Directory.Move(finalRoot, quarantine);
                }
                throw;
            }
            _ = ResearchWorkspaceFullTextGenerationVerifier.VerifyCurrent(location, committed, journal, handoff, maximumBytes, conductPolicy);
            return new ResearchWorkspaceFullTextCommit(committed, manifest, false);
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
        }
    }
}

public sealed class ResearchWorkspaceFullTextScreeningCommitPort(
    ResearchWorkspaceLocation location,
    ResearchWorkspaceProject expectedProject,
    ScreeningConductJournal admissionJournal,
    ScreeningConductHandoff admissionHandoff,
    VerifiedFullTextAdmission admission,
    VerifiedFullTextChain authority,
    byte[] rawBytes,
    long maximumBytes,
    FullTextExtractionAttempt? extractionAttempt = null) : IFullTextScreeningConductCommitPort
{
    public FullTextScreeningConductCommitResult Commit(
        FullTextScreeningConductPolicy policy,
        FullTextScreeningConductHeader header,
        IReadOnlyList<IFullTextScreeningConductEntry> entries)
    {
        var journal = FullTextScreeningConductJournal.RehydrateEntries(header, policy, entries);
        var records = new List<ResearchWorkspaceFullTextRecord>
        {
            Record("conduct-policy", policy.ToCanonicalJson()),
            Record("conduct-header", header.ToCanonicalJson())
        };
        records.AddRange(entries.Select((entry, index) => new ResearchWorkspaceFullTextRecord(
            $"conduct-entry-{index + 1:D6}", SerializeEntry(entry))));
        if (journal.Projection.HandoffReady)
            records.Add(Record("conduct-handoff", journal.CreateHandoff(
                $"fulltext-handoff-{journal.Projection.HeadDigest.Value[7..19]}", EntryTimestamp(entries[^1])).ToCanonicalJson()));
        var commit = ResearchWorkspaceFullTextTransaction.Commit(
            location, expectedProject, admissionJournal, admissionHandoff, admission, authority,
            rawBytes, maximumBytes, extractionAttempt, records, policy);
        return new FullTextScreeningConductCommitResult(header.ConductId, journal.Projection.HeadDigest, entries.Count, commit.AlreadyApplied);
    }

    private static ResearchWorkspaceFullTextRecord Record(string name, CanonicalJsonObject value) =>
        new(name, CanonicalJsonSerializer.SerializeToUtf8Bytes(value));

    private static byte[] SerializeEntry(IFullTextScreeningConductEntry entry) => entry switch
    {
        FullTextScreeningConductDecision decision => CanonicalJsonSerializer.SerializeToUtf8Bytes(decision.ToCanonicalJson()),
        FullTextScreeningConductInvalidation invalidation => CanonicalJsonSerializer.SerializeToUtf8Bytes(invalidation.ToCanonicalJson()),
        _ => throw new InvalidOperationException("Unknown Full Text conduct entry type.")
    };

    private static DateTimeOffset EntryTimestamp(IFullTextScreeningConductEntry entry) => entry switch
    {
        FullTextScreeningConductDecision decision => decision.DecidedAt,
        FullTextScreeningConductInvalidation invalidation => invalidation.InvalidatedAt,
        _ => throw new InvalidOperationException("Unknown Full Text conduct entry type.")
    };
}
