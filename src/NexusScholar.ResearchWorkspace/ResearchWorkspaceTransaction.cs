using System.Text.Json;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Provenance;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceTransaction
{
    public static ResearchWorkspaceAuthorityTransitionCommit CommitDeduplicationDecision(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationReviewCommand command,
        VerifiedDeduplicationAuthorityReviewTargetDigest target,
        IClock clock,
        IIdGenerator idGenerator,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(expectedProject);
        ArgumentNullException.ThrowIfNull(sourceResult);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(idGenerator);

        VerifySourceResultBinding(location, expectedProject, sourceResult);
        var predecessorChain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(location, expectedProject, sourceResult);
        var replay = FindReplay(predecessorChain, command);
        if (replay is not null)
        {
            return FromReplay(expectedProject, replay);
        }

        if (!string.Equals(command.Material.AuthorityGenerationId, predecessorChain.GenerationId, StringComparison.Ordinal) ||
            command.Material.AuthorityGenerationManifestDigest.ToString() != expectedProject.AuthorityGenerationManifestSha256 ||
            !string.Equals(command.Material.SourceSnapshotId, predecessorChain.CurrentSnapshot.SnapshotId, StringComparison.Ordinal) ||
            command.Material.SourceSnapshotRecordDigest != predecessorChain.CurrentSnapshot.RecordDigest ||
            command.Material.ActiveDecisionSetDigest != predecessorChain.CurrentSnapshot.DecisionSetDigest)
        {
            throw new ResearchWorkspaceAuthorityTransitionException(
                ResearchWorkspaceAuthorityTransitionException.StaleAuthorityCategory,
                "The review command does not target the current authority generation.");
        }

        var policy = predecessorChain.Policy;
        var decision = DeduplicationDecision.CreateDecisionMaterial(
            DeduplicationReviewCommand.BuildDecisionMaterial(command, target), clock, policy, sourceResult, target);
        var activeDecisions = predecessorChain.ActiveDecisions
            .Where(item => command.Material.SupersedesDecisionId is null ||
                !string.Equals(item.DecisionId, command.Material.SupersedesDecisionId, StringComparison.Ordinal))
            .Append(decision)
            .ToArray();
        var knownDecisions = predecessorChain.KnownDecisions.Append(decision).ToArray();
        var invalidationReferences = decision.InvalidationEffects.Select(effect => new CorpusSnapshotInvalidationReference(
            effect.RecordKind, effect.RecordId, effect.RecordDigest)).ToArray();
        var digestHex = command.RequestDigest.ToString()["sha256:".Length..];
        var snapshot = CorpusSnapshotService.CreateSuccessor(
            $"snapshot-{digestHex}", predecessorChain.CurrentSnapshot, policy,
            command.Material.ActorId, command.Material.ActorRole, clock, activeDecisions,
            invalidationReferences, knownDecisions, predecessorChain.KnownSnapshots, sourceResult, decision);
        var knownSnapshots = predecessorChain.KnownSnapshots.Append(snapshot).ToArray();
        var invalidation = CorpusSnapshotInvalidation.CreateInvalidationMaterial(
            new UnverifiedCorpusSnapshotInvalidation(
                CorpusSnapshotInvalidationConstants.SchemaId,
                CorpusSnapshotInvalidationConstants.SchemaVersion,
                $"invalidation-{digestHex}",
                decision.DecisionId,
                decision.DecisionDigest,
                snapshot.SnapshotId,
                snapshot.RecordDigest,
                decision.InvalidationEffects.Select(effect => new CorpusSnapshotInvalidationInvalidatedRecordReference(
                    effect.RecordKind, effect.RecordId, effect.RecordDigest)).ToArray(),
                command.Material.ActorId,
                command.Material.ActorRole,
                policy.PolicyId,
                DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
                policy.PolicyDigest,
                clock.UtcNow),
            clock, policy, decision, snapshot, knownDecisions, knownSnapshots);
        var events = BuildSuccessorEvents(command, target, sourceResult, policy, predecessorChain.CurrentSnapshot,
            decision, snapshot, invalidation, clock, idGenerator);

        var authorityGenerationId = $"authority-{digestHex}";
        var generationRelative = ResearchWorkspacePaths.AuthorityGenerationRoot(authorityGenerationId);
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory,
            $"{ResearchWorkspacePaths.GenerationStaging}/{authorityGenerationId}-{Guid.NewGuid():N}");
        var generationRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, generationRelative);
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var records = new Dictionary<string, (string FileName, byte[] Bytes)>(StringComparer.Ordinal)
            {
                ["authority-policy"] = ("authority-policy.json", ResearchWorkspaceAuthorityArtifacts.SerializePolicyCanonicalRecord(policy)),
                ["decision"] = ("decision.json", ResearchWorkspaceAuthorityArtifacts.SerializeDecisionCanonicalRecord(decision)),
                ["decision-recorded-event"] = ("decision-recorded-event.json", ResearchWorkspaceAuthorityArtifacts.SerializeResearchEventCanonicalRecord(events.DecisionRecorded)),
                ["invalidation"] = ("invalidation.json", ResearchWorkspaceAuthorityArtifacts.SerializeInvalidationCanonicalRecord(invalidation)),
                ["review-command"] = ("review-command.json", ResearchWorkspaceAuthorityArtifacts.SerializeReviewCommandCanonicalRecord(command)),
                ["snapshot-invalidated-event"] = ("snapshot-invalidated-event.json", ResearchWorkspaceAuthorityArtifacts.SerializeResearchEventCanonicalRecord(events.SnapshotInvalidated)),
                ["snapshot-publication-event"] = ("snapshot-publication-event.json", ResearchWorkspaceAuthorityArtifacts.SerializeResearchEventCanonicalRecord(events.SnapshotPublished)),
                ["successor-snapshot"] = ("successor-snapshot.json", ResearchWorkspaceAuthorityArtifacts.SerializeSnapshotCanonicalRecord(snapshot))
            };
            var artifacts = records.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
            {
                File.WriteAllBytes(Path.Combine(stagingRoot, item.Value.FileName), item.Value.Bytes);
                return new ResearchWorkspaceGenerationArtifact(item.Key,
                    $"{generationRelative}/{item.Value.FileName}", ContentDigest.Sha256(item.Value.Bytes).ToString());
            }).ToArray();

            var manifestPath = $"{generationRelative}/authority-generation.manifest.json";
            var committedProject = expectedProject
                .ClearSuccessorBoundDownstreamCurrentState()
                .CommitAuthorityGeneration(authorityGenerationId, manifestPath, "sha256:" + new string('0', 64));
            var manifest = new ResearchWorkspaceSuccessorAuthorityGenerationManifest(
                ResearchWorkspaceSuccessorAuthorityGenerationManifest.CurrentSchema,
                authorityGenerationId,
                expectedProject.WorkspaceId,
                committedProject.Revision,
                ResearchWorkspaceSuccessorAuthorityGenerationManifest.DeduplicationDecisionTransition,
                expectedProject.CurrentGenerationId!,
                ContentDigest.Sha256(File.ReadAllBytes(ResolveRequiredPath(location, expectedProject.GenerationManifestPath!))).ToString(),
                sourceResult.Result.ResultId,
                sourceResult.ResultDigest.ToString(),
                predecessorChain.GenerationId,
                expectedProject.AuthorityGenerationManifestSha256!,
                command.RequestId,
                command.RequestDigest.ToString(),
                policy.PolicyId,
                policy.PolicyDigest.ToString(),
                decision.DecisionId,
                decision.DecisionDigest.ToString(),
                predecessorChain.CurrentSnapshot.SnapshotId,
                predecessorChain.CurrentSnapshot.RecordDigest.ToString(),
                snapshot.SnapshotId,
                snapshot.ContentDigest.ToString(),
                snapshot.RecordDigest.ToString(),
                invalidation.InvalidationId,
                invalidation.RecordDigest.ToString(),
                snapshot.DecisionSetDigest.ToString(),
                events.DecisionRecorded.EventDigest.ToString(),
                events.SnapshotInvalidated.EventDigest.ToString(),
                events.SnapshotPublished.EventDigest.ToString(),
                artifacts);
            var manifestBytes = ResearchWorkspaceSuccessorAuthorityManifestCodec.Serialize(manifest);
            var manifestDigest = ContentDigest.Sha256(manifestBytes);
            committedProject = committedProject with { AuthorityGenerationManifestSha256 = manifestDigest.ToString() };
            File.WriteAllBytes(Path.Combine(stagingRoot, "authority-generation.manifest.json"), manifestBytes);
            VerifyStagedSuccessor(stagingRoot, generationRelative, manifestBytes, manifest);
            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterStaging);

            Directory.CreateDirectory(Path.GetDirectoryName(generationRoot)!);
            using var workspaceLock = AcquireLock(location);
            var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            RecoverOrphanedAuthorityGenerations(location, currentProject);
            VerifySourceResultBinding(location, currentProject, sourceResult);
            var currentChain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(location, currentProject, sourceResult);
            replay = FindReplay(currentChain, command);
            if (replay is not null)
            {
                return FromReplay(currentProject, replay);
            }
            if (currentProject.Revision != expectedProject.Revision ||
                !string.Equals(currentProject.CurrentAuthorityGenerationId, expectedProject.CurrentAuthorityGenerationId, StringComparison.Ordinal) ||
                !string.Equals(currentProject.AuthorityGenerationManifestSha256, expectedProject.AuthorityGenerationManifestSha256, StringComparison.Ordinal))
            {
                throw new ResearchWorkspaceAuthorityTransitionException(
                    ResearchWorkspaceAuthorityTransitionException.StaleAuthorityCategory,
                    "The authority generation advanced before this request could commit.");
            }

            Directory.Move(stagingRoot, generationRoot);
            try
            {
                faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterPromotion);
                ResearchWorkspaceStore.WriteProject(location, committedProject);
            }
            catch
            {
                Quarantine(location, generationRoot, authorityGenerationId);
                throw;
            }

            return new ResearchWorkspaceAuthorityTransitionCommit(
                committedProject, manifest, command, decision, snapshot, invalidation,
                events.DecisionRecorded, events.SnapshotInvalidated, events.SnapshotPublished, AlreadyApplied: false);
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
        }
    }

    public static ResearchWorkspaceAuthorityCommit InitializeAuthorityGeneration(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        string expectedAnalysisGenerationId,
        string expectedAnalysisManifestSha256,
        string snapshotId,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        string publisherActorId,
        string publisherRole,
        IClock clock,
        IIdGenerator idGenerator,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(expectedProject);
        ArgumentNullException.ThrowIfNull(sourceResult);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(idGenerator);
        RejectActiveAuthority(expectedProject);

        if (!string.Equals(expectedProject.CurrentGenerationId, expectedAnalysisGenerationId, StringComparison.Ordinal) ||
            expectedProject.GenerationManifestPath is null ||
            !ContentDigest.TryParse(expectedAnalysisManifestSha256, out var expectedManifestDigest))
        {
            throw new ResearchWorkspaceConcurrencyException("The expected analysis generation binding is stale or malformed.", new InvalidOperationException());
        }

        var sourceManifestPath = ResolveRequiredPath(location, expectedProject.GenerationManifestPath);
        if (ContentDigest.Sha256(File.ReadAllBytes(sourceManifestPath)) != expectedManifestDigest)
        {
            throw new ResearchWorkspaceConcurrencyException("The expected analysis manifest digest is stale.", new InvalidOperationException());
        }
        VerifySourceResultBinding(location, expectedProject, sourceResult);

        var baseline = CorpusSnapshotService.CreateBaseline(
            snapshotId,
            sourceResult,
            policy,
            publisherActorId,
            publisherRole,
            clock);
        var publishedEvent = BuildBaselinePublicationEvent(
            expectedProject,
            expectedAnalysisGenerationId,
            expectedManifestDigest,
            sourceResult,
            policy,
            baseline,
            publisherActorId,
            clock,
            idGenerator);

        var authorityGenerationId = $"authority-{Guid.NewGuid():N}";
        var generationRelative = ResearchWorkspacePaths.AuthorityGenerationRoot(authorityGenerationId);
        var stagingRoot = ResearchWorkspacePaths.InProject(
            location.RootDirectory,
            $"{ResearchWorkspacePaths.GenerationStaging}/{authorityGenerationId}");
        var generationRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, generationRelative);
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var policyBytes = ResearchWorkspaceAuthorityArtifacts.SerializePolicyCanonicalRecord(policy);
            var snapshotBytes = ResearchWorkspaceAuthorityArtifacts.SerializeSnapshotCanonicalRecord(baseline);
            var eventBytes = ResearchWorkspaceAuthorityArtifacts.SerializeResearchEventCanonicalRecord(publishedEvent);
            var artifacts = new[]
            {
                WriteCanonicalArtifact("authority-policy", "authority-policy.json", policyBytes),
                WriteCanonicalArtifact("baseline-snapshot", "baseline-snapshot.json", snapshotBytes),
                WriteCanonicalArtifact("snapshot-publication-event", "snapshot-publication-event.json", eventBytes)
            }.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();

            _ = ResearchWorkspaceAuthorityArtifacts.VerifyPolicyCanonicalRecord(policyBytes);
            _ = ResearchWorkspaceAuthorityArtifacts.VerifySnapshotCanonicalRecord(snapshotBytes, sourceResult, policy);
            _ = ResearchWorkspaceAuthorityArtifacts.VerifyResearchEventCanonicalRecord(eventBytes);

            var manifestPath = $"{generationRelative}/authority-generation.manifest.json";
            var committedProject = expectedProject.CommitAuthorityGeneration(
                authorityGenerationId,
                manifestPath,
                "sha256:" + new string('0', 64));
            var manifest = new ResearchWorkspaceAuthorityGenerationManifest(
                ResearchWorkspaceAuthorityGenerationManifest.CurrentSchema,
                authorityGenerationId,
                expectedProject.WorkspaceId,
                committedProject.Revision,
                expectedAnalysisGenerationId,
                expectedManifestDigest.ToString(),
                sourceResult.Result.ResultId,
                sourceResult.ResultDigest.ToString(),
                null,
                null,
                policy.PolicyId,
                policy.PolicyDigest.ToString(),
                baseline.DecisionSetDigest.ToString(),
                artifacts);
            var manifestBytes = SerializeAuthorityManifest(manifest);
            var manifestDigest = ContentDigest.Sha256(manifestBytes);
            committedProject = committedProject with { AuthorityGenerationManifestSha256 = manifestDigest.ToString() };
            File.WriteAllBytes(Path.Combine(stagingRoot, "authority-generation.manifest.json"), manifestBytes);
            VerifyStagedAuthorityManifest(stagingRoot, generationRelative, manifestBytes, manifest);
            faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterStaging);

            Directory.CreateDirectory(Path.GetDirectoryName(generationRoot)!);
            using var workspaceLock = AcquireLock(location);
            var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            RecoverOrphanedAuthorityGenerations(location, currentProject);
            RejectActiveAuthority(currentProject);
            if (currentProject.Revision != expectedProject.Revision ||
                !string.Equals(currentProject.WorkspaceId, expectedProject.WorkspaceId, StringComparison.Ordinal) ||
                !string.Equals(currentProject.CurrentGenerationId, expectedAnalysisGenerationId, StringComparison.Ordinal) ||
                !string.Equals(currentProject.GenerationManifestPath, expectedProject.GenerationManifestPath, StringComparison.Ordinal))
            {
                throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, currentProject.Revision);
            }

            var lockedManifestPath = ResolveRequiredPath(location, currentProject.GenerationManifestPath!);
            if (ContentDigest.Sha256(File.ReadAllBytes(lockedManifestPath)) != expectedManifestDigest)
            {
                throw new ResearchWorkspaceConcurrencyException("The source analysis manifest changed during authority initialization.", new InvalidOperationException());
            }
            VerifySourceResultBinding(location, currentProject, sourceResult);

            Directory.Move(stagingRoot, generationRoot);
            try
            {
                faultInjector?.Invoke(ResearchWorkspaceAuthorityFaultPoint.AfterPromotion);
                ResearchWorkspaceStore.WriteProject(location, committedProject);
            }
            catch
            {
                Quarantine(location, generationRoot, authorityGenerationId);
                throw;
            }

            return new ResearchWorkspaceAuthorityCommit(committedProject, manifest, baseline, publishedEvent);

            ResearchWorkspaceGenerationArtifact WriteCanonicalArtifact(string name, string fileName, byte[] bytes)
            {
                var path = Path.Combine(stagingRoot, fileName);
                File.WriteAllBytes(path, bytes);
                return new ResearchWorkspaceGenerationArtifact(
                    name,
                    $"{generationRelative}/{fileName}",
                    ContentDigest.Sha256(bytes).ToString());
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

    public static ResearchWorkspaceProject CommitImport(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        ResearchWorkspaceInput input,
        byte[] sourceBytes,
        SearchImportTrace trace,
        string sourceExtension)
    {
        RejectActiveAuthority(expectedProject);
        var importId = $"import-{Guid.NewGuid():N}";
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, $"{ResearchWorkspacePaths.GenerationStaging}/{importId}");
        var importRelative = $"{ResearchWorkspacePaths.SearchInputs}/{input.EffectiveInputId}";
        var importRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, importRelative);
        var sourceRelative = $"{importRelative}/source.{sourceExtension}";
        var traceRelative = $"{importRelative}/import-trace.json";
        Directory.CreateDirectory(stagingRoot);
        try
        {
            File.WriteAllBytes(Path.Combine(stagingRoot, $"source.{sourceExtension}"), sourceBytes);
            ResearchWorkspaceJson.WriteJsonFile(Path.Combine(stagingRoot, "import-trace.json"), trace);
            var committedInput = input with { RelativePath = sourceRelative, ImportTracePath = traceRelative };
            var committedProject = expectedProject.WithInput(committedInput) with
            {
                Revision = checked(expectedProject.Revision + 1),
                Outputs = new Dictionary<string, string>(StringComparer.Ordinal),
                CurrentGenerationId = null,
                GenerationManifestPath = null
            };

            Directory.CreateDirectory(Path.GetDirectoryName(importRoot)!);
            using var workspaceLock = AcquireLock(location);
            var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            RejectActiveAuthority(currentProject);
            if (currentProject.Revision != expectedProject.Revision)
            {
                throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, currentProject.Revision);
            }

            Directory.Move(stagingRoot, importRoot);
            try
            {
                ResearchWorkspaceStore.WriteProject(location, committedProject);
            }
            catch
            {
                Quarantine(location, importRoot, importId);
                throw;
            }

            return committedProject;
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    public static ResearchWorkspaceAnalysisCommit AnalyzeAndCommit(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        Action<ResearchWorkspaceAnalysisFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(expectedProject);
        RejectActiveAuthority(expectedProject);

        using var workspaceLock = AcquireLock(location);
        var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        RejectActiveAuthority(currentProject);
        if (currentProject.Revision != expectedProject.Revision ||
            !string.Equals(currentProject.WorkspaceId, expectedProject.WorkspaceId, StringComparison.Ordinal) ||
            !currentProject.Inputs.SequenceEqual(expectedProject.Inputs))
        {
            throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, currentProject.Revision);
        }

        faultInjector?.Invoke(ResearchWorkspaceAnalysisFaultPoint.AfterLockAcquired);
        using var inputSnapshots = AcquireDeclaredInputSnapshots(location, expectedProject, faultInjector);
        faultInjector?.Invoke(ResearchWorkspaceAnalysisFaultPoint.AfterInputSnapshotsAcquired);
        var analysis = ResearchWorkspaceAnalyzer.Analyze(location, expectedProject, inputSnapshots.Bytes);
        var generationId = $"gen-{Guid.NewGuid():N}";
        var stagingRelative = $"{ResearchWorkspacePaths.GenerationStaging}/{generationId}";
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, stagingRelative);
        var generationRelative = ResearchWorkspacePaths.GenerationRoot(generationId);
        var generationRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, generationRelative);
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var traceArtifacts = WriteTraces(stagingRoot, generationRelative, analysis);
            var outputArtifacts = WriteOutputs(stagingRoot, generationRelative, analysis);
            var updatedInputs = expectedProject.Inputs.Select(input =>
            {
                var trace = traceArtifacts.Single(artifact => string.Equals(artifact.Name, input.EffectiveInputId, StringComparison.Ordinal));
                return input with { ImportTracePath = trace.RelativePath };
            }).ToArray();
            var outputs = outputArtifacts.ToDictionary(artifact => artifact.Name, artifact => artifact.RelativePath, StringComparer.Ordinal);
            var manifestPath = $"{generationRelative}/generation.manifest.json";
            var committedProject = (expectedProject with { Inputs = updatedInputs }).CommitGeneration(outputs, generationId, manifestPath);
            var manifest = new ResearchWorkspaceGenerationManifest(
                ResearchWorkspaceGenerationManifest.CurrentSchema,
                generationId,
                expectedProject.WorkspaceId,
                committedProject.Revision,
                expectedProject.Inputs.OrderBy(input => input.EffectiveInputId, StringComparer.Ordinal)
                    .Select(input => new ResearchWorkspaceGenerationArtifact(input.EffectiveInputId, input.EffectiveRelativePath, input.Sha256)).ToArray(),
                traceArtifacts,
                outputArtifacts);
            ResearchWorkspaceJson.WriteJsonFile(Path.Combine(stagingRoot, "generation.manifest.json"), manifest);

            Directory.CreateDirectory(Path.GetDirectoryName(generationRoot)!);
            Directory.Move(stagingRoot, generationRoot);
            try
            {
                faultInjector?.Invoke(ResearchWorkspaceAnalysisFaultPoint.AfterPromotionBeforeFinalInputValidation);
                ValidateDeclaredInputsNotMutated(location, expectedProject);
                ResearchWorkspaceStore.WriteProject(location, committedProject);
            }
            catch
            {
                if (Directory.Exists(generationRoot))
                {
                    Quarantine(location, generationRoot, generationId);
                }

                throw;
            }

            return new ResearchWorkspaceAnalysisCommit(analysis, committedProject, manifest);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static DeclaredInputSnapshotLease AcquireDeclaredInputSnapshots(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        Action<ResearchWorkspaceAnalysisFaultPoint>? faultInjector)
    {
        var streams = new List<FileStream>();
        var snapshots = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (var input in project.Inputs)
            {
                var stream = OpenDeclaredInputLease(
                    location,
                    input,
                    () => faultInjector?.Invoke(ResearchWorkspaceAnalysisFaultPoint.AfterInputPathValidatedBeforeOpen));
                streams.Add(stream);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var bytes = buffer.ToArray();
                if (ContentDigest.Sha256(bytes).ToString() != input.Sha256)
                {
                    throw new ResearchWorkspaceDigestMismatchException(
                        $"Input digest mismatch: {input.EffectiveRelativePath}");
                }

                if (!snapshots.TryAdd(input.EffectiveInputId, bytes))
                {
                    throw new InvalidOperationException("Declared input identifiers must be unique.");
                }
            }

            return new DeclaredInputSnapshotLease(streams, snapshots);
        }
        catch
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }

            throw;
        }
    }

    private static FileStream OpenDeclaredInputLease(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceInput input,
        Action? afterPathValidation)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(
                location.RootDirectory,
                input.EffectiveRelativePath,
                out var sourcePath) ||
            !File.Exists(sourcePath))
        {
            throw new ResearchWorkspaceMissingInputException("A declared input is missing.");
        }

        afterPathValidation?.Invoke();

        FileStream stream;
        try
        {
            stream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            throw new ResearchWorkspaceMissingInputException("A declared input is missing.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new ResearchWorkspaceMissingInputException("A declared input is missing.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ResearchWorkspaceConcurrencyException(
                "A declared input could not be leased for immutable analysis.",
                exception);
        }

        if (!ResearchWorkspaceVerifier.IsOpenFileAtExpectedPath(stream, sourcePath) ||
            !ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(
                location.RootDirectory,
                input.EffectiveRelativePath,
                out var revalidatedPath) ||
            !File.Exists(revalidatedPath) ||
            !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(revalidatedPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            stream.Dispose();
            throw new ResearchWorkspaceConcurrencyException(
                "A declared input path changed while its immutable analysis lease was acquired.",
                new IOException("The opened file handle does not resolve to the admitted workspace path."));
        }

        return stream;
    }

    private static IReadOnlyList<ResearchWorkspaceGenerationArtifact> WriteTraces(
        string stagingRoot,
        string generationRelative,
        ResearchWorkspaceAnalysisResult analysis)
    {
        var artifacts = new List<ResearchWorkspaceGenerationArtifact>();
        foreach (var trace in analysis.ImportTraces.OrderBy(trace => trace.TraceId, StringComparer.Ordinal))
        {
            var inputId = trace.TraceId.EndsWith(".import-trace", StringComparison.Ordinal)
                ? trace.TraceId[..^".import-trace".Length]
                : trace.TraceId;
            var local = $"imports/{inputId}.import-trace.json";
            var path = Path.Combine(stagingRoot, local.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ResearchWorkspaceJson.WriteJsonFile(path, trace);
            artifacts.Add(Artifact(inputId, $"{generationRelative}/{local}", path));
        }

        return artifacts;
    }

    private static IReadOnlyList<ResearchWorkspaceGenerationArtifact> WriteOutputs(
        string stagingRoot,
        string generationRelative,
        ResearchWorkspaceAnalysisResult analysis)
    {
        var files = new[]
        {
            Write("deduplicationResult", "dedup/current.deduplication-result.json", value => ResearchWorkspaceJson.WriteJsonFile(value, analysis.DeduplicationResult)),
            Write("workspacePlan", "workspace/current.workspace-plan.json", value => ResearchWorkspaceJson.WriteJsonFile(value, analysis.WorkspacePlan, UiContractJson.SerializerOptions)),
            Write("reviewReport", "reports/current.review-report.md", value => ResearchWorkspaceJson.WriteTextFile(value, WorkspacePlanReportWriter.Format(analysis)))
        };
        return files;

        ResearchWorkspaceGenerationArtifact Write(string name, string local, Action<string> writer)
        {
            var path = Path.Combine(stagingRoot, local.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            writer(path);
            return Artifact(name, $"{generationRelative}/{local}", path);
        }
    }

    private static ResearchWorkspaceGenerationArtifact Artifact(string name, string relativePath, string path) =>
        new(name, relativePath, ContentDigest.Sha256(File.ReadAllBytes(path)).ToString());

    private static void ValidateDeclaredInputsNotMutated(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        foreach (var input in project.Inputs)
        {
            using var stream = OpenDeclaredInputLease(location, input, afterPathValidation: null);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            if (ContentDigest.Sha256(buffer.ToArray()).ToString() != input.Sha256)
            {
                throw new ResearchWorkspaceDigestMismatchException("A declared input digest changed before analysis publication.");
            }
        }
    }

    private static string ResolveRequiredPath(ResearchWorkspaceLocation location, string relativePath)
    {
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var path) || !File.Exists(path))
        {
            throw new ResearchWorkspaceMissingInputException("The source analysis manifest is missing or outside the workspace.");
        }

        return path;
    }

    private static void VerifySourceResultBinding(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        VerifiedDeduplicationAuthorityResultDigest expectedSourceResult)
    {
        var analysisManifestPath = ResolveRequiredPath(location, project.GenerationManifestPath!);
        using var analysisManifest = JsonDocument.Parse(File.ReadAllBytes(analysisManifestPath));
        var analysisRevision = analysisManifest.RootElement.GetProperty("projectRevision").GetInt64();
        var analysisProject = project with { Revision = analysisRevision };
        var generation = ResearchWorkspaceGenerationVerifier.VerifyCurrent(location, analysisProject)
            ?? throw new ResearchWorkspaceMissingInputException("A committed analysis generation is required before authority initialization.");
        var resultArtifact = generation.Outputs.SingleOrDefault(item => string.Equals(item.Name, "deduplicationResult", StringComparison.Ordinal))
            ?? throw new ResearchWorkspaceMissingInputException("The analysis generation does not contain a deduplication result.");
        var resultPath = ResolveRequiredPath(location, resultArtifact.RelativePath);
        var persisted = JsonSerializer.Deserialize<DeduplicationResult>(
            File.ReadAllBytes(resultPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("The committed deduplication result did not contain an object.");
        var verified = DeduplicationAuthorityDigests.CreateResultDigestMaterial(persisted);
        if (!string.Equals(verified.Result.ResultId, expectedSourceResult.Result.ResultId, StringComparison.Ordinal) ||
            verified.ResultDigest != expectedSourceResult.ResultDigest)
        {
            throw new ResearchWorkspaceConcurrencyException("The supplied source result does not match the committed analysis generation.", new InvalidOperationException());
        }
    }

    private static void RecoverOrphanedAuthorityGenerations(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject currentProject)
    {
        var root = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.AuthorityGenerations);
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var generationId = Path.GetFileName(directory);
            if (!string.Equals(generationId, currentProject.CurrentAuthorityGenerationId, StringComparison.Ordinal))
            {
                Quarantine(location, directory, generationId);
            }
        }
    }

    private static ResearchWorkspaceVerifiedAuthorityTransition? FindReplay(
        ResearchWorkspaceVerifiedAuthorityChain chain,
        VerifiedDeduplicationReviewCommand command)
    {
        var matches = chain.Transitions.Where(item => item.Command.RequestDigest == command.RequestDigest).ToArray();
        if (matches.Length > 1)
        {
            throw new ResearchWorkspaceAuthorityTransitionException(
                ResearchWorkspaceAuthorityTransitionException.ConflictingReplayCategory,
                "The authority chain contains duplicate records for one request digest.");
        }
        if (matches.Length == 1 && (!string.Equals(matches[0].Command.RequestId, command.RequestId, StringComparison.Ordinal) ||
            !string.Equals(matches[0].Decision.DecisionId, command.DecisionId, StringComparison.Ordinal)))
        {
            throw new ResearchWorkspaceAuthorityTransitionException(
                ResearchWorkspaceAuthorityTransitionException.ConflictingReplayCategory,
                "The persisted request digest resolves to different authority identities.");
        }
        return matches.SingleOrDefault();
    }

    private static ResearchWorkspaceAuthorityTransitionCommit FromReplay(
        ResearchWorkspaceProject project,
        ResearchWorkspaceVerifiedAuthorityTransition transition) =>
        new(project, transition.Manifest, transition.Command, transition.Decision, transition.Snapshot,
            transition.Invalidation, transition.DecisionRecordedEvent, transition.SnapshotInvalidatedEvent,
            transition.SnapshotPublicationEvent, AlreadyApplied: true);

    private static (ResearchEvent DecisionRecorded, ResearchEvent SnapshotInvalidated, ResearchEvent SnapshotPublished) BuildSuccessorEvents(
        VerifiedDeduplicationReviewCommand command,
        VerifiedDeduplicationAuthorityReviewTargetDigest target,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedCorpusSnapshot predecessor,
        VerifiedDeduplicationAuthorityDecision decision,
        VerifiedCorpusSnapshot successor,
        VerifiedCorpusSnapshotInvalidation invalidation,
        IClock clock,
        IIdGenerator idGenerator)
    {
        var agent = new ProvenanceAgent(command.Material.ActorId, ProvenanceAgent.HumanKind);
        var commandRef = new ProvenanceEntityRef(DeduplicationReviewCommandConstants.SchemaId, command.RequestId, command.RequestDigest);
        var targetRef = new ProvenanceEntityRef(target.TargetKind, target.TargetId, target.TargetDigest);
        var resultRef = new ProvenanceEntityRef("nexus.deduplication.result", sourceResult.Result.ResultId, sourceResult.ResultDigest);
        var policyRef = new ProvenanceEntityRef(DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, policy.PolicyId, policy.PolicyDigest);
        var predecessorRef = new ProvenanceEntityRef("nexus.corpus.snapshot", predecessor.SnapshotId, predecessor.RecordDigest);
        var decisionRef = new ProvenanceEntityRef(DeduplicationDecisionConstants.SchemaId, decision.DecisionId, decision.DecisionDigest);
        var successorRef = new ProvenanceEntityRef("nexus.corpus.snapshot", successor.SnapshotId, successor.RecordDigest);
        var invalidationRef = new ProvenanceEntityRef(CorpusSnapshotInvalidationConstants.SchemaId, invalidation.InvalidationId, invalidation.RecordDigest);
        var decisionSetRef = new ProvenanceEntityRef("deduplication-decision-set", $"decision-set-{successor.SnapshotId}", successor.DecisionSetDigest);

        var decisionRecorded = ResearchEventFactory.Create(idGenerator, clock,
            new ProvenanceActivity("deduplication-decision-recorded", "Deduplication decision recorded", true, true, true),
            decisionRef, agent,
            new[] { commandRef, targetRef, resultRef, policyRef, predecessorRef },
            new[] { decisionRef });
        var snapshotInvalidated = ResearchEventFactory.Create(idGenerator, clock,
            new ProvenanceActivity("corpus-snapshot-invalidated", "Corpus snapshot invalidated", true, true, true),
            predecessorRef, agent,
            new[] { decisionRef, predecessorRef, successorRef },
            new[] { invalidationRef });
        var snapshotPublished = ResearchEventFactory.Create(idGenerator, clock,
            new ProvenanceActivity("corpus-snapshot-published", "Corpus snapshot published", true, true, true),
            successorRef, agent,
            new[] { decisionRef, predecessorRef, invalidationRef, policyRef, decisionSetRef },
            new[] { successorRef });
        return (decisionRecorded, snapshotInvalidated, snapshotPublished);
    }

    private static void VerifyStagedSuccessor(
        string stagingRoot,
        string generationRelative,
        byte[] manifestBytes,
        ResearchWorkspaceSuccessorAuthorityGenerationManifest manifest)
    {
        var parsed = ResearchWorkspaceSuccessorAuthorityManifestCodec.ParseCanonical(manifestBytes);
        if (!ResearchWorkspaceSuccessorAuthorityManifestCodec.Serialize(parsed).SequenceEqual(manifestBytes))
        {
            throw new InvalidOperationException("Staged successor authority manifest did not round-trip exactly.");
        }
        foreach (var artifact in manifest.Artifacts)
        {
            var prefix = generationRelative + "/";
            if (!artifact.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Successor authority artifact path escaped its generation.");
            }
            var localPath = artifact.RelativePath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var bytes = File.ReadAllBytes(Path.Combine(stagingRoot, localPath));
            if (ContentDigest.Sha256(bytes).ToString() != artifact.Sha256)
            {
                throw new InvalidOperationException($"Successor authority artifact '{artifact.Name}' failed staged verification.");
            }
        }
    }

    private static ResearchEvent BuildBaselinePublicationEvent(
        ResearchWorkspaceProject project,
        string analysisGenerationId,
        ContentDigest analysisManifestDigest,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedCorpusSnapshot snapshot,
        string publisherActorId,
        IClock clock,
        IIdGenerator idGenerator)
    {
        var snapshotRef = new ProvenanceEntityRef("nexus.corpus.snapshot", snapshot.SnapshotId, snapshot.RecordDigest);
        return ResearchEventFactory.Create(
            idGenerator,
            clock,
            new ProvenanceActivity("corpus-snapshot-published", "Corpus snapshot published", true, true, true),
            snapshotRef,
            new ProvenanceAgent(publisherActorId, ProvenanceAgent.HumanKind),
            new[]
            {
                new ProvenanceEntityRef("nexus.deduplication.result", sourceResult.Result.ResultId, sourceResult.ResultDigest),
                new ProvenanceEntityRef(DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, policy.PolicyId, policy.PolicyDigest),
                new ProvenanceEntityRef("source-analysis-manifest", analysisGenerationId, analysisManifestDigest),
                new ProvenanceEntityRef("deduplication-decision-set", "decision-set-empty", snapshot.DecisionSetDigest)
            },
            new[] { snapshotRef });
    }

    private static byte[] SerializeAuthorityManifest(ResearchWorkspaceAuthorityGenerationManifest manifest)
    {
        var canonical = new CanonicalJsonObject()
            .Add("schema", manifest.Schema)
            .Add("authority_generation_id", manifest.AuthorityGenerationId)
            .Add("workspace_id", manifest.WorkspaceId)
            .Add("project_revision", manifest.ProjectRevision)
            .Add("source_analysis_generation_id", manifest.SourceAnalysisGenerationId)
            .Add("source_analysis_manifest_sha256", manifest.SourceAnalysisManifestSha256)
            .Add("source_result_id", manifest.SourceResultId)
            .Add("source_result_digest", manifest.SourceResultDigest)
            .Add("predecessor_authority_generation_id", CanonicalJsonValue.Null())
            .Add("predecessor_authority_generation_manifest_sha256", CanonicalJsonValue.Null())
            .Add("authority_policy_id", manifest.AuthorityPolicyId)
            .Add("authority_policy_digest", manifest.AuthorityPolicyDigest)
            .Add("decision_set_digest", manifest.DecisionSetDigest)
            .Add("artifacts", CanonicalJsonValue.Array(manifest.Artifacts.OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("name", item.Name)
                    .Add("relative_path", item.RelativePath)
                    .Add("sha256", item.Sha256)).ToArray()));
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(canonical);
    }

    private static void VerifyStagedAuthorityManifest(
        string stagingRoot,
        string generationRelative,
        byte[] manifestBytes,
        ResearchWorkspaceAuthorityGenerationManifest manifest)
    {
        using var document = System.Text.Json.JsonDocument.Parse(manifestBytes);
        var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
        if (!manifestBytes.SequenceEqual(canonical) || manifest.Artifacts.Count != 3)
        {
            throw new InvalidOperationException("Authority generation manifest is not canonical or complete.");
        }

        foreach (var artifact in manifest.Artifacts.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var prefix = generationRelative + "/";
            if (!artifact.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Authority artifact path is outside the staged generation.");
            }

            var local = artifact.RelativePath[prefix.Length..];
            var path = Path.Combine(stagingRoot, local.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || ContentDigest.Sha256(File.ReadAllBytes(path)).ToString() != artifact.Sha256)
            {
                throw new InvalidOperationException($"Authority artifact '{artifact.Name}' failed staged verification.");
            }
        }
    }

    private static void RejectActiveAuthority(ResearchWorkspaceProject project)
    {
        if (project.CurrentAuthorityGenerationId is not null)
        {
            throw new ResearchWorkspaceAuthorityGenerationActiveException();
        }
    }

    private sealed class DeclaredInputSnapshotLease : IDisposable
    {
        private readonly IReadOnlyList<FileStream> streams;

        public DeclaredInputSnapshotLease(
            IReadOnlyList<FileStream> streams,
            IReadOnlyDictionary<string, byte[]> bytes)
        {
            this.streams = streams;
            Bytes = bytes;
        }

        public IReadOnlyDictionary<string, byte[]> Bytes { get; }

        public void Dispose()
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
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

    private static void Quarantine(ResearchWorkspaceLocation location, string generationRoot, string generationId)
    {
        var quarantine = ResearchWorkspacePaths.InProject(location.RootDirectory, $"{ResearchWorkspacePaths.GenerationQuarantine}/{generationId}");
        Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
        Directory.Move(generationRoot, quarantine);
    }
}

public enum ResearchWorkspaceAuthorityFaultPoint
{
    AfterStaging,
    AfterPromotion
}

public enum ResearchWorkspaceAnalysisFaultPoint
{
    AfterLockAcquired,
    AfterInputPathValidatedBeforeOpen,
    AfterInputSnapshotsAcquired,
    AfterPromotionBeforeFinalInputValidation
}

public sealed record ResearchWorkspaceAuthorityCommit(
    ResearchWorkspaceProject Project,
    ResearchWorkspaceAuthorityGenerationManifest Manifest,
    VerifiedCorpusSnapshot BaselineSnapshot,
    ResearchEvent PublicationEvent);

public sealed record ResearchWorkspaceAuthorityTransitionCommit(
    ResearchWorkspaceProject Project,
    ResearchWorkspaceSuccessorAuthorityGenerationManifest Manifest,
    VerifiedDeduplicationReviewCommand Command,
    VerifiedDeduplicationAuthorityDecision Decision,
    VerifiedCorpusSnapshot Snapshot,
    VerifiedCorpusSnapshotInvalidation Invalidation,
    ResearchEvent DecisionRecordedEvent,
    ResearchEvent SnapshotInvalidatedEvent,
    ResearchEvent SnapshotPublicationEvent,
    bool AlreadyApplied);

public sealed class ResearchWorkspaceConcurrencyException : InvalidOperationException
{
    public ResearchWorkspaceConcurrencyException(long expectedRevision, long actualRevision)
        : base($"Workspace revision changed during mutation. Expected {expectedRevision}; found {actualRevision}.")
    {
    }

    public ResearchWorkspaceConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
