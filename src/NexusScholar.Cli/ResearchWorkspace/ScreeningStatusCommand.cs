using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ScreeningStatusCommand
{
    internal static int Run(TextWriter output, TextWriter error, string workingDirectory)
    {
        try
        {
            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                error.WriteLine("No Nexus research workspace found in the current folder or its parents.");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }
            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (project.ScreeningConductManifestPath is null || project.ScreeningConductManifestSha256 is null)
            {
                output.WriteLine("Screening conduct: not initialized");
                return ResearchWorkspaceExitCodes.Success;
            }
            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(
                location.RootDirectory, project.ScreeningConductManifestPath, out var path) || !File.Exists(path))
                throw new InvalidOperationException("Screening conduct manifest is missing or outside the workspace.");
            var bytes = File.ReadAllBytes(path);
            if (ContentDigest.Sha256(bytes).ToString() != project.ScreeningConductManifestSha256)
                throw new InvalidOperationException("Screening conduct manifest failed project-pointer digest verification.");
            var manifest = ResearchWorkspaceScreeningConductManifestCodec.Rehydrate(bytes);
            foreach (var artifact in manifest.Artifacts)
            {
                if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(
                    location.RootDirectory, artifact.RelativePath, out var artifactPath) || !File.Exists(artifactPath))
                    throw new InvalidOperationException($"Screening conduct artifact '{artifact.Name}' is missing or outside the workspace.");
                if (ContentDigest.Sha256(File.ReadAllBytes(artifactPath)).ToString() != artifact.Sha256)
                    throw new InvalidOperationException($"Screening conduct artifact '{artifact.Name}' failed digest verification.");
            }
            output.WriteLine("Screening conduct status");
            output.WriteLine($"Conduct: {manifest.ConductId}");
            output.WriteLine($"Generation: {manifest.GenerationId}");
            output.WriteLine($"Head: {manifest.ResultingHeadDigest}");
            output.WriteLine($"Entries: {manifest.EntryCount}");
            output.WriteLine($"Decisions: {manifest.DecisionCount}");
            output.WriteLine($"Invalidations: {manifest.InvalidationCount}");
            output.WriteLine($"Handoff: {manifest.HandoffId ?? "not-ready"}");
            output.WriteLine("Verification: manifest-and-artifact-integrity-only (authority not rehydrated)");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            error.WriteLine($"Unable to read Screening conduct status: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }
}
