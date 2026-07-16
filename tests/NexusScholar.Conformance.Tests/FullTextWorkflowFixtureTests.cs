using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.FullText;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class FullTextWorkflowFixtureTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Extraction_cases_execute_the_local_no_network_contract()
    {
        var path = Path.Combine(FindRepositoryRoot(), "fixtures", "conformance", "fulltext-workflow", "extraction-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.AreEqual("local-csharp-contract", root.GetProperty("scope").GetString());
        CollectionAssert.Contains(
            root.GetProperty("non_claims").EnumerateArray().Select(item => item.GetString()).ToArray(),
            "no-pdf-parser-claim");

        foreach (var item in root.GetProperty("cases").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            var kind = item.GetProperty("artifact_kind").GetString()!;
            var mediaType = item.GetProperty("media_type").GetString()!;
            var bytes = Encoding.UTF8.GetBytes(item.GetProperty("content").GetString()!);
            var input = FullTextInput.FromScreeningDecision(
                $"input-{id}", "candidate-set-fixture", $"candidate-{id}", $"decision-{id}",
                "title_abstract", FullTextScreeningVerdicts.Include);
            var acquisition = new FullTextAcquisitionRecord(
                $"acquisition-{id}", input, FullTextAcquisitionKinds.ManualAcquisition, "fixture", "fixture-bytes",
                new FullTextActor("fixture-human", FullTextActorKinds.Human), FixedTime, FullTextAttemptStatuses.Success,
                [new FullTextSourceAttempt($"source-attempt-{id}", "fixture", 1, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success)]);
            var artifact = FullTextArtifactEvidence.FromBytes(
                $"artifact-{id}", input, acquisition, kind, mediaType, bytes, 4096);
            var source = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 4096));

            var attempt = FullTextDeterministicExtractor.Extract($"extract-{id}", source, bytes, FixedTime);
            var reopened = FullTextExtractionAttemptCodec.Rehydrate(
                FullTextExtractionAttemptCodec.Serialize(attempt), attempt.Digest, source);

            Assert.AreEqual(item.GetProperty("expected_status").GetString(), reopened.Status, id);
            if (item.TryGetProperty("expected_output", out var output))
                Assert.AreEqual(output.GetString(), reopened.Values.Single(), id);
            if (item.TryGetProperty("expected_failure_category", out var category))
                Assert.AreEqual(category.GetString(), reopened.FailureCategory, id);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
