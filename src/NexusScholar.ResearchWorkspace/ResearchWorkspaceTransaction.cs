using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceTransaction
{
    public static ResearchWorkspaceProject CommitImport(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        ResearchWorkspaceInput input,
        byte[] sourceBytes,
        SearchImportTrace trace,
        string sourceExtension)
    {
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
        ResearchWorkspaceProject expectedProject)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(expectedProject);

        var analysis = ResearchWorkspaceAnalyzer.Analyze(location, expectedProject);
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
            using var workspaceLock = AcquireLock(location);
            var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (currentProject.Revision != expectedProject.Revision ||
                !string.Equals(currentProject.WorkspaceId, expectedProject.WorkspaceId, StringComparison.Ordinal))
            {
                throw new ResearchWorkspaceConcurrencyException(expectedProject.Revision, currentProject.Revision);
            }

            Directory.Move(stagingRoot, generationRoot);
            try
            {
                ResearchWorkspaceStore.WriteProject(location, committedProject);
            }
            catch
            {
                Quarantine(location, generationRoot, generationId);
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
