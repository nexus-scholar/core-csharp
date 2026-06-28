using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.UiContracts;

namespace NexusScholar.UiContracts.Tests;

[TestClass]
public sealed class UiContractTests
{
    [TestMethod]
    public void Research_block_descriptor_rejects_empty_id()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => NewBlock(blockId: " "));

        Assert.AreEqual("blockId", exception.ParamName);
    }

    [TestMethod]
    public void Research_block_descriptor_rejects_empty_kind()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => NewBlock(kind: " "));

        Assert.AreEqual("kind", exception.ParamName);
    }

    [TestMethod]
    public void Research_block_descriptor_rejects_empty_title()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => NewBlock(title: " "));

        Assert.AreEqual("title", exception.ParamName);
    }

    [TestMethod]
    public void Evidence_ref_rejects_empty_kind_and_value()
    {
        var emptyKind = Assert.ThrowsExactly<ArgumentException>(() => new EvidenceRef(" ", "trace-1"));
        var emptyValue = Assert.ThrowsExactly<ArgumentException>(() => new EvidenceRef(KnownEvidenceRefKinds.SearchTrace, " "));

        Assert.AreEqual("kind", emptyKind.ParamName);
        Assert.AreEqual("value", emptyValue.ParamName);
    }

    [TestMethod]
    public void Block_action_descriptor_rejects_empty_action_id_and_label()
    {
        var emptyId = Assert.ThrowsExactly<ArgumentException>(() => new BlockActionDescriptor(
            " ",
            BlockActionKind.ShowDetails,
            "Show details",
            requiresHumanConfirmation: false,
            isDestructive: false));
        var emptyLabel = Assert.ThrowsExactly<ArgumentException>(() => new BlockActionDescriptor(
            "show-details",
            BlockActionKind.ShowDetails,
            " ",
            requiresHumanConfirmation: false,
            isDestructive: false));

        Assert.AreEqual("actionId", emptyId.ParamName);
        Assert.AreEqual("label", emptyLabel.ParamName);
    }

    [TestMethod]
    public void Workspace_plan_preserves_block_order()
    {
        var plan = new WorkspacePlan(
            "workspace-dedup",
            "Dedup review",
            BlockMode.Review,
            new[]
            {
                NewBlock("block-1", KnownBlockKinds.ImportSummary, "Import"),
                NewBlock("block-2", KnownBlockKinds.DedupCandidateCluster, "Cluster"),
                NewBlock("block-3", KnownBlockKinds.HumanGateMergeDecision, "Decision")
            });

        CollectionAssert.AreEqual(
            new[] { "block-1", "block-2", "block-3" },
            plan.Blocks.Select(block => block.BlockId).ToArray());
    }

    [TestMethod]
    public void Workspace_plan_json_round_trip_preserves_optional_fields()
    {
        var plan = new WorkspacePlan(
            "workspace-import",
            "Import review",
            BlockMode.Beginner,
            new[] { NewBlock(payloadJson: "{\"parsedRecords\":42}") },
            "Review imported warnings",
            new[] { new EvidenceRef(KnownEvidenceRefKinds.ImportSource, "import-source-1", "Import", "sha256:abc", "raw-artifact-bytes") });

        var json = JsonSerializer.Serialize(plan, UiContractJson.SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<WorkspacePlan>(json, UiContractJson.SerializerOptions);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(plan.WorkspaceId, roundTrip.WorkspaceId);
        Assert.AreEqual(plan.Description, roundTrip.Description);
        Assert.AreEqual(1, roundTrip.ContextRefs.Count);
        Assert.AreEqual("sha256:abc", roundTrip.ContextRefs[0].Digest);
        Assert.AreEqual("{\"parsedRecords\":42}", roundTrip.Blocks[0].PayloadJson);
    }

    [TestMethod]
    public void Payload_json_must_be_valid_object_when_present()
    {
        var invalid = Assert.ThrowsExactly<ArgumentException>(() => NewBlock(payloadJson: "{"));
        var nonObject = Assert.ThrowsExactly<ArgumentException>(() => NewBlock(payloadJson: "[1,2,3]"));

        Assert.AreEqual("payloadJson", invalid.ParamName);
        Assert.AreEqual("payloadJson", nonObject.ParamName);
    }

    [TestMethod]
    public void Sample_import_warning_block_round_trips()
    {
        var block = new ResearchBlockDescriptor(
            "block.import.warning.001",
            KnownBlockKinds.ImportWarningSummary,
            "Rows without stable identifiers",
            BlockMode.Beginner,
            BlockSeverity.Warning,
            BlockSourceKind.Sample,
            new[]
            {
                new EvidenceRef(
                    KnownEvidenceRefKinds.ImportSource,
                    "import-source-001",
                    "Imported export",
                    "sha256:example-source-file-digest",
                    "raw-artifact-bytes")
            },
            new[]
            {
                new ValidationRef(
                    "missing-stable-identifier",
                    BlockSeverity.ReviewRequired,
                    "Imported rows without stable identifiers require later review.",
                    "row-7")
            },
            new[]
            {
                new BlockActionDescriptor(
                    "review-warning-records",
                    BlockActionKind.ShowDetails,
                    "Review warning records",
                    requiresHumanConfirmation: false,
                    isDestructive: false,
                    KnownBlockActionCommandKinds.ShowDetails,
                    "import-source-001")
            },
            "Some imported rows need attention before later workflow steps rely on them.",
            "{\"warningRecords\":3,\"rawEvidencePreserved\":true}");

        var roundTrip = RoundTrip(block);

        Assert.AreEqual(KnownBlockKinds.ImportWarningSummary, roundTrip.Kind);
        Assert.AreEqual(BlockSeverity.Warning, roundTrip.Severity);
        Assert.AreEqual(1, roundTrip.EvidenceRefs.Count);
        Assert.AreEqual(1, roundTrip.ValidationRefs.Count);
        Assert.AreEqual(1, roundTrip.Actions.Count);
    }

    [TestMethod]
    public void Sample_dedup_review_block_round_trips()
    {
        var block = new ResearchBlockDescriptor(
            "block.cluster.001",
            KnownBlockKinds.DedupCandidateCluster,
            "Candidate cluster requires review",
            BlockMode.Review,
            BlockSeverity.ReviewRequired,
            BlockSourceKind.Sample,
            new[]
            {
                new EvidenceRef(
                    KnownEvidenceRefKinds.DeduplicationResult,
                    "dedup-result-001",
                    "Dedup result",
                    "sha256:example-dedup-result-digest",
                    "canonical-json-record")
            },
            Array.Empty<ValidationRef>(),
            new[]
            {
                new BlockActionDescriptor(
                    "accept-merge",
                    BlockActionKind.AcceptMerge,
                    "Accept merge",
                    requiresHumanConfirmation: true,
                    isDestructive: false,
                    KnownBlockActionCommandKinds.AcceptMerge,
                    "candidate-cluster-001"),
                new BlockActionDescriptor(
                    "mark-unresolved",
                    BlockActionKind.MarkUnresolved,
                    "Mark unresolved",
                    requiresHumanConfirmation: true,
                    isDestructive: false,
                    KnownBlockActionCommandKinds.MarkUnresolved,
                    "candidate-cluster-001")
            },
            "Title-only duplicate evidence requires human review.",
            "{\"clusterId\":\"candidate-cluster-001\",\"reviewRequired\":true}");

        var roundTrip = RoundTrip(block);

        Assert.AreEqual(KnownBlockKinds.DedupCandidateCluster, roundTrip.Kind);
        Assert.AreEqual(BlockSeverity.ReviewRequired, roundTrip.Severity);
        Assert.AreEqual(2, roundTrip.Actions.Count);
        Assert.IsTrue(roundTrip.Actions.All(action => action.RequiresHumanConfirmation));
    }

    [TestMethod]
    public void Ui_contracts_has_no_avalonia_mobile_web_or_terminal_network_dependency()
    {
        var forbiddenPrefixes = new[]
        {
            string.Concat("Ava", "lonia"),
            string.Concat("Microsoft.", "Maui"),
            string.Concat("Microsoft.", "AspNetCore"),
            string.Concat("System.", "Net.", "Http")
        };
        var forbidden = typeof(WorkspacePlan).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.AreEqual(0, forbidden.Length, $"Forbidden references: {string.Join(", ", forbidden)}");
    }

    private static ResearchBlockDescriptor RoundTrip(ResearchBlockDescriptor block)
    {
        var json = JsonSerializer.Serialize(block, UiContractJson.SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<ResearchBlockDescriptor>(json, UiContractJson.SerializerOptions);
        Assert.IsNotNull(roundTrip);

        return roundTrip;
    }

    private static ResearchBlockDescriptor NewBlock(
        string blockId = "block-1",
        string kind = KnownBlockKinds.ImportSummary,
        string title = "Import summary",
        string? payloadJson = null) =>
        new(
            blockId,
            kind,
            title,
            BlockMode.Beginner,
            BlockSeverity.Info,
            BlockSourceKind.Sample,
            Array.Empty<EvidenceRef>(),
            Array.Empty<ValidationRef>(),
            Array.Empty<BlockActionDescriptor>(),
            null,
            payloadJson);
}
