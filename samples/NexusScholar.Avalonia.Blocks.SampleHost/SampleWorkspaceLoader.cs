using System.Text.Json;
using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks.SampleHost;

public sealed record SampleWorkspace(string FileName, string DisplayName, WorkspacePlan Plan);

public static class SampleWorkspaceLoader
{
    public static readonly IReadOnlyList<string> ExpectedSampleFileNames = new[]
    {
        "import-warning.sample.json",
        "dedup-review.sample.json",
        "bundle-verification.sample.json"
    };

    public static IReadOnlyList<SampleWorkspace> LoadDefaultSamples()
    {
        return LoadFromRepositoryRoot(FindRepositoryRoot(AppContext.BaseDirectory));
    }

    public static IReadOnlyList<SampleWorkspace> LoadFromRepositoryRoot(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var sampleDirectory = Path.Combine(repositoryRoot, "samples", "block-plans");
        return ExpectedSampleFileNames
            .Select(fileName => LoadSample(sampleDirectory, fileName))
            .ToArray();
    }

    public static string FindRepositoryRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static SampleWorkspace LoadSample(string sampleDirectory, string fileName)
    {
        var path = Path.Combine(sampleDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Expected sample WorkspacePlan file was not found.", path);
        }

        var json = File.ReadAllText(path);
        var plan = JsonSerializer.Deserialize<WorkspacePlan>(json, UiContractJson.SerializerOptions)
            ?? throw new InvalidOperationException($"Sample WorkspacePlan did not deserialize: {path}");

        return new SampleWorkspace(fileName, plan.Title, plan);
    }
}
