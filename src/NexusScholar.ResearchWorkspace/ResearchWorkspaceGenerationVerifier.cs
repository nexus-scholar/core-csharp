using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceGenerationVerifier
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ResearchWorkspaceGenerationManifest? VerifyCurrent(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        if (project.CurrentGenerationId is null)
        {
            return null;
        }

        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, project.GenerationManifestPath, out var manifestPath) ||
            !File.Exists(manifestPath))
        {
            throw new InvalidOperationException("The committed generation manifest is missing or outside the workspace.");
        }

        var manifest = JsonSerializer.Deserialize<ResearchWorkspaceGenerationManifest>(File.ReadAllText(manifestPath), Options)
            ?? throw new JsonException("Generation manifest did not contain an object.");
        if (!string.Equals(manifest.Schema, ResearchWorkspaceGenerationManifest.CurrentSchema, StringComparison.Ordinal) ||
            !string.Equals(manifest.GenerationId, project.CurrentGenerationId, StringComparison.Ordinal) ||
            !string.Equals(manifest.WorkspaceId, project.WorkspaceId, StringComparison.Ordinal) ||
            manifest.ProjectRevision != project.Revision)
        {
            throw new InvalidOperationException("The committed generation manifest is stale or belongs to another workspace.");
        }

        var expectedInputs = project.Inputs.OrderBy(input => input.EffectiveInputId, StringComparer.Ordinal).ToArray();
        if (manifest.Inputs.Count != expectedInputs.Length ||
            manifest.Inputs.Where((artifact, index) =>
                !string.Equals(artifact.Name, expectedInputs[index].EffectiveInputId, StringComparison.Ordinal) ||
                !string.Equals(artifact.RelativePath, expectedInputs[index].EffectiveRelativePath, StringComparison.Ordinal) ||
                !string.Equals(artifact.Sha256, expectedInputs[index].Sha256, StringComparison.Ordinal)).Any())
        {
            throw new InvalidOperationException("The committed generation does not bind the current ordered inputs.");
        }

        foreach (var artifact in manifest.ImportTraces)
        {
            var path = VerifyArtifact(location, artifact);
            using var trace = JsonDocument.Parse(File.ReadAllText(path));
            if (trace.RootElement.ValueKind != JsonValueKind.Object ||
                !trace.RootElement.TryGetProperty("traceId", out var traceId) ||
                string.IsNullOrWhiteSpace(traceId.GetString()) ||
                !trace.RootElement.TryGetProperty("metadata", out var metadata) ||
                metadata.ValueKind != JsonValueKind.Object ||
                !trace.RootElement.TryGetProperty("importedRecords", out var records) ||
                records.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException($"Generation import trace '{artifact.Name}' is structurally invalid.");
            }
        }

        foreach (var artifact in manifest.Outputs)
        {
            VerifyArtifact(location, artifact);
        }

        foreach (var output in project.Outputs)
        {
            if (!manifest.Outputs.Any(artifact => string.Equals(artifact.Name, output.Key, StringComparison.Ordinal) &&
                string.Equals(artifact.RelativePath, output.Value, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Project output '{output.Key}' is not bound by the committed generation.");
            }
        }

        return manifest;
    }

    private static string VerifyArtifact(ResearchWorkspaceLocation location, ResearchWorkspaceGenerationArtifact artifact)
    {
        if (!ContentDigest.TryParse(artifact.Sha256, out _) ||
            !ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, artifact.RelativePath, out var path) ||
            !File.Exists(path))
        {
            throw new InvalidOperationException($"Generation artifact '{artifact.Name}' is missing, malformed, or outside the workspace.");
        }

        var actual = ContentDigest.Sha256(File.ReadAllBytes(path)).ToString();
        if (!string.Equals(actual, artifact.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Generation artifact '{artifact.Name}' failed digest verification.");
        }

        return path;
    }
}
