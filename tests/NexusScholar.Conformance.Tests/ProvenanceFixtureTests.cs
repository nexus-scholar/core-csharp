using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Provenance;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ProvenanceFixtureTests
{
    private const string FixtureSourceKind = "local-gate-5-contract";
    private const string FixtureSourceCommit = "pending-gate-5-implementation-commit";
    private static readonly IClock Clock = new FixedClock();

    private static readonly string[] RequiredFixtureIds =
    {
        "provenance-event-protocol-approved",
        "provenance-event-workflow-node-completed",
        "provenance-ledger-append-order",
        "provenance-ledger-duplicate-reject",
        "provenance-invalid-missing-actor",
        "provenance-invalid-missing-required-input",
        "provenance-invalid-missing-required-output",
        "provenance-invalid-projection-as-canonical"
    };

    [TestMethod]
    public void Gate_5_provenance_fixtures_are_present()
    {
        var ids = Directory.GetFiles(ProvenanceFixtureDirectory(), "*.*")
            .Where(path => path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".ndjson", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixtureId in RequiredFixtureIds)
        {
            Assert.IsTrue(ids.Contains(fixtureId), $"Missing Gate 5 provenance fixture '{fixtureId}'.");
        }
    }

    [TestMethod]
    public void Gate_5_json_fixtures_have_required_local_metadata()
    {
        foreach (var path in Directory.GetFiles(ProvenanceFixtureDirectory(), "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var fixtureId = root.GetProperty("fixtureId").GetString();

            Assert.AreEqual(FixtureSourceKind, root.GetProperty("sourceKind").GetString(), fixtureId);
            Assert.AreEqual(FixtureSourceCommit, root.GetProperty("sourceCommit").GetString(), fixtureId);
            Assert.AreEqual("hand-authored local Gate 5 provenance fixture", root.GetProperty("generatorCommand").GetString(), fixtureId);
            Assert.AreEqual("gate-5-v1", root.GetProperty("generatorVersion").GetString(), fixtureId);
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0008-provenance-ledger.md", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-blueprint-conformance-claim", StringComparison.Ordinal)), fixtureId);
            _ = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            _ = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);
        }
    }

    [TestMethod]
    public void Positive_provenance_event_fixtures_replay_expected_event_digest()
    {
        foreach (var fixtureId in new[] { "provenance-event-protocol-approved", "provenance-event-workflow-node-completed" })
        {
            using var document = LoadJsonFixture($"{fixtureId}.json");
            var root = document.RootElement;
            var fixtureCase = root.GetProperty("case");
            var expected = BuildEvent(fixtureId);

            Assert.IsFalse(fixtureCase.GetProperty("negative").GetBoolean(), fixtureId);
            Assert.AreEqual("nexus.provenance-event", fixtureCase.GetProperty("schemaId").GetString(), fixtureId);
            Assert.AreEqual("1.0.0", fixtureCase.GetProperty("schemaVersion").GetString(), fixtureId);
            Assert.AreEqual("provenance-event", fixtureCase.GetProperty("digestScope").GetString(), fixtureId);
            Assert.AreEqual(expected.EventId.ToString(), fixtureCase.GetProperty("event").GetProperty("event_id").GetString(), fixtureId);
            Assert.AreEqual(expected.EventDigest.ToString(), fixtureCase.GetProperty("event").GetProperty("event_digest").GetString(), fixtureId);
            Assert.AreEqual(expected.EventDigest.ToString(), root.GetProperty("outputDigest").GetString(), fixtureId);
            Assert.IsTrue(fixtureCase.GetProperty("nonClaims").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(fixtureCase.GetProperty("nonClaims").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-blueprint-conformance-claim", StringComparison.Ordinal)), fixtureId);
        }
    }

    [TestMethod]
    public void Provenance_append_order_fixture_replays_ledger_order()
    {
        var path = Path.Combine(ProvenanceFixtureDirectory(), "provenance-ledger-append-order.ndjson");
        var lines = File.ReadAllLines(path);
        var store = new InMemoryProvenanceStore();
        var expectedOrder = new List<string>();

        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            Assert.AreEqual(FixtureSourceKind, root.GetProperty("sourceKind").GetString());
            Assert.AreEqual(FixtureSourceCommit, root.GetProperty("sourceCommit").GetString());
            Assert.AreEqual("append", root.GetProperty("operation").GetString());

            var fixtureId = root.GetProperty("eventFixtureId").GetString()!;
            var record = BuildEvent(fixtureId);
            store.Append(record);
            expectedOrder.Add(root.GetProperty("eventId").GetString()!);
        }

        CollectionAssert.AreEqual(expectedOrder, store.ReadAll().Select(item => item.EventId.ToString()).ToArray());
    }

    [TestMethod]
    public void Negative_provenance_fixtures_replay_expected_error_categories()
    {
        AssertNegativeFixture(
            "provenance-ledger-duplicate-reject.json",
            ProvenanceErrorCodes.DuplicateEventId,
            () =>
            {
                var first = BuildEvent("provenance-event-protocol-approved");
                var second = BuildEvent("provenance-event-protocol-approved");
                var store = new InMemoryProvenanceStore();
                store.Append(first);
                store.Append(second);
            });

        AssertNegativeFixture(
            "provenance-invalid-missing-actor.json",
            ProvenanceErrorCodes.MissingActor,
            () => ResearchEventFactory.Create(
                new FixedIdGenerator(new Guid("00000000-0000-0000-0000-000000000511")),
                Clock,
                new ProvenanceActivity("protocol-approved", "Protocol approved", true, false, false),
                new ProvenanceEntityRef("protocol-version", "protocol-version-1"),
                new ProvenanceAgent(string.Empty, ProvenanceAgent.HumanKind)));

        AssertNegativeFixture(
            "provenance-invalid-missing-required-input.json",
            ProvenanceErrorCodes.MissingRequiredInput,
            () => ResearchEventFactory.Create(
                new FixedIdGenerator(new Guid("00000000-0000-0000-0000-000000000512")),
                Clock,
                new ProvenanceActivity("workflow-node-started", "Workflow node started", false, true, false),
                new ProvenanceEntityRef("workflow-node", "workflow-1:node-search"),
                new ProvenanceAgent("automation-1", ProvenanceAgent.Automation),
                inputs: new[] { new ProvenanceEntityRef("protocol-version", "protocol-version-1") }));

        AssertNegativeFixture(
            "provenance-invalid-missing-required-output.json",
            ProvenanceErrorCodes.MissingRequiredOutput,
            () => ResearchEventFactory.Create(
                new FixedIdGenerator(new Guid("00000000-0000-0000-0000-000000000513")),
                Clock,
                new ProvenanceActivity("artifact-created", "Artifact created", false, false, true),
                new ProvenanceEntityRef("artifact", "artifact-1"),
                new ProvenanceAgent("automation-1", ProvenanceAgent.Automation),
                outputs: new[] { new ProvenanceEntityRef("artifact", "artifact-1") }));

        AssertNegativeFixture(
            "provenance-invalid-projection-as-canonical.json",
            ProvenanceErrorCodes.ProjectionNotCanonical,
            () => ResearchEventFactory.Create(
                new FixedIdGenerator(new Guid("00000000-0000-0000-0000-000000000514")),
                Clock,
                new ProvenanceActivity("projection-refreshed", "Projection refreshed", false, false, false),
                new ProvenanceEntityRef("projection", "projection-1"),
                new ProvenanceAgent("automation-1", ProvenanceAgent.Automation)));
    }

    private static void AssertNegativeFixture(string fileName, string category, Action action)
    {
        using var document = LoadJsonFixture(fileName);
        var root = document.RootElement;
        Assert.AreEqual(category, root.GetProperty("case").GetProperty("errorCategory").GetString(), fileName);

        var exception = Assert.ThrowsExactly<ProvenanceRuleException>(action);
        Assert.AreEqual(category, exception.Category, fileName);
    }

    private static ResearchEvent BuildEvent(string fixtureId)
    {
        return fixtureId switch
        {
            "provenance-event-protocol-approved" => ResearchEventFactory.Create(
                new FixedIdGenerator(new Guid("00000000-0000-0000-0000-000000000501")),
                Clock,
                new ProvenanceActivity("protocol-approved", "Protocol approved", true, false, false),
                new ProvenanceEntityRef("protocol-version", "protocol-version-1", ContentDigest.Sha256Utf8("gate-5-protocol-version")),
                new ProvenanceAgent("researcher-1", ProvenanceAgent.HumanKind, "Researcher 1"),
                protocolBinding: new ProvenanceProtocolBinding(
                    "protocol-1",
                    "protocol-version-1",
                    1,
                    ContentDigest.Sha256Utf8("gate-5-protocol-content"))),
            "provenance-event-workflow-node-completed" => ResearchEventFactory.Create(
                new FixedIdGenerator(new Guid("00000000-0000-0000-0000-000000000502")),
                Clock,
                new ProvenanceActivity("workflow-node-completed", "Workflow node completed", false, true, true),
                new ProvenanceEntityRef("workflow-node", "workflow-1:node-search"),
                new ProvenanceAgent("automation-1", ProvenanceAgent.Automation, "Local automation"),
                inputs: new[]
                {
                    new ProvenanceEntityRef("protocol-version", "protocol-version-1", ContentDigest.Sha256Utf8("gate-5-protocol-content"))
                },
                outputs: new[]
                {
                    new ProvenanceEntityRef("workflow-artifact", "search-plan", ContentDigest.Sha256Utf8("gate-5-search-plan"))
                },
                workflowBinding: new ProvenanceWorkflowBinding(
                    "workflow-1",
                    ContentDigest.Sha256Utf8("gate-5-workflow-definition"),
                    "node-search")),
            _ => throw new InvalidOperationException($"Unknown Gate 5 provenance fixture '{fixtureId}'.")
        };
    }

    private static JsonDocument LoadJsonFixture(string fileName)
    {
        var path = Path.Combine(ProvenanceFixtureDirectory(), fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ProvenanceFixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "provenance");

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly Guid _id;

        public FixedIdGenerator(Guid id)
        {
            _id = id;
        }

        public Guid NewId() => _id;
    }
}
