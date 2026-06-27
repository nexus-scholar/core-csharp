using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Provenance;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ProvenanceTests
{
    [TestMethod]
    public void Append_event_success()
    {
        var store = new InMemoryProvenanceStore();
        var eventRecord = CreateEventRecord(
            new Guid("00000000-0000-0000-0000-000000000001"),
            "protocol-approved");

        store.Append(eventRecord);

        Assert.AreEqual(1, store.ReadAll().Count);
        Assert.AreEqual(eventRecord.EventId, store.ReadAll()[0].EventId);
    }

    [TestMethod]
    public void Duplicate_event_id_rejected()
    {
        var ids = new FixedIdGenerator(
            Guid.Parse("00000000-0000-0000-0000-000000000010"),
            Guid.Parse("00000000-0000-0000-0000-000000000010"));
        var clock = new FixedClock();
        var store = new InMemoryProvenanceStore();

        var first = CreateEventRecord(ids, clock, new ProvenanceActivity("protocol-approved", "Protocol approved", false, false, false));
        var second = CreateEventRecord(ids, clock, new ProvenanceActivity("protocol-approved", "Protocol approved", false, false, false));

        store.Append(first);
        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() => store.Append(second));
        Assert.AreEqual(ProvenanceErrorCodes.DuplicateEventId, error.Category);
    }

    [TestMethod]
    public void Missing_actor_rejected()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();

        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() =>
            ResearchEventFactory.Create(
                ids,
                clock,
                new ProvenanceActivity("protocol-approved", "Protocol approved", RequiresActor: true, RequiresInput: false, RequiresOutput: false),
                new ProvenanceEntityRef("protocol-version", "protocol-1"),
                new ProvenanceAgent(string.Empty, "human"),
                protocolBinding: null));

        Assert.AreEqual(ProvenanceErrorCodes.MissingActor, error.Category);
    }

    [TestMethod]
    public void Missing_required_input_digest_rejected()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();

        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() =>
            ResearchEventFactory.Create(
                ids,
                clock,
                new ProvenanceActivity("data-loaded", "Data loaded", RequiresActor: false, RequiresInput: true, RequiresOutput: false),
                new ProvenanceEntityRef("dataset", "dataset-1"),
                new ProvenanceAgent("automation-1", "automation"),
                inputs: new[] { new ProvenanceEntityRef("dataset", "dataset-1") },
                protocolBinding: null));

        Assert.AreEqual(ProvenanceErrorCodes.MissingRequiredInput, error.Category);
    }

    [TestMethod]
    public void Missing_required_output_digest_rejected()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();

        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() =>
            ResearchEventFactory.Create(
                ids,
                clock,
                new ProvenanceActivity("data-exported", "Data exported", RequiresActor: false, RequiresInput: false, RequiresOutput: true),
                new ProvenanceEntityRef("dataset", "dataset-2"),
                new ProvenanceAgent("automation-1", "automation"),
                outputs: new[] { new ProvenanceEntityRef("dataset", "dataset-2") },
                protocolBinding: null));

        Assert.AreEqual(ProvenanceErrorCodes.MissingRequiredOutput, error.Category);
    }

    [TestMethod]
    public void Event_digest_uses_provenance_event_scope()
    {
        var record = CreateEventRecord(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "protocol-approved");

        var expectedEnvelope = record.ToDigestEnvelope();

        Assert.AreEqual("provenance-event", expectedEnvelope.Scope.Value);
        Assert.AreEqual("nexus.provenance-event", expectedEnvelope.SchemaId);
        Assert.AreEqual("1.0.0", expectedEnvelope.SchemaVersion);
        Assert.AreEqual(expectedEnvelope.ComputeDigest(), record.EventDigest);
    }

    [TestMethod]
    public void Protocol_version_binding_preserved()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();
        var protocolDigest = ContentDigest.Sha256Utf8("protocol-content");
        var subjectBinding = new ProvenanceProtocolBinding(
            "protocol-1",
            "protocol-version-1",
            2,
            protocolDigest);

        var record = ResearchEventFactory.Create(
            ids,
            clock,
            new ProvenanceActivity("protocol-approved", "Protocol approved", RequiresActor: false, RequiresInput: false, RequiresOutput: false),
            new ProvenanceEntityRef("protocol-version", "protocol-version-1"),
            new ProvenanceAgent("researcher-1", "human"),
            protocolBinding: subjectBinding);

        Assert.AreEqual("protocol-1", record.ProtocolBinding?.ProtocolId);
        Assert.AreEqual("protocol-version-1", record.ProtocolBinding?.ProtocolVersionId);
        Assert.AreEqual(2, record.ProtocolBinding?.ProtocolVersionNumber);
        Assert.AreEqual(protocolDigest, record.ProtocolBinding?.ProtocolContentDigest);
    }

    [TestMethod]
    public void Workflow_binding_id_and_node_preserved()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();
        var workflowDigest = ContentDigest.Sha256Utf8("workflow");
        var workflowBinding = new ProvenanceWorkflowBinding(
            "workflow-1",
            workflowDigest,
            WorkflowNodeId: "node-9");

        var record = ResearchEventFactory.Create(
            ids,
            clock,
            new ProvenanceActivity("workflow-node-completed", "Workflow node completed", false, false, false),
            new ProvenanceEntityRef("workflow", "workflow-1"),
            new ProvenanceAgent("automation-1", "automation"),
            workflowBinding: workflowBinding);

        Assert.AreEqual("workflow-1", record.WorkflowBinding?.WorkflowId);
        Assert.AreEqual(workflowDigest, record.WorkflowBinding?.WorkflowDigest);
        Assert.AreEqual("node-9", record.WorkflowBinding?.WorkflowNodeId);
    }

    [TestMethod]
    public void Event_collections_are_immutable()
    {
        var store = new InMemoryProvenanceStore();
        var first = CreateEventRecord(Guid.Parse("00000000-0000-0000-0000-000000000003"), "protocol-approved");
        var second = CreateEventRecord(Guid.Parse("00000000-0000-0000-0000-000000000004"), "protocol-approved");
        var snapshot = store.ReadAll();

        store.Append(first);
        store.Append(second);
        var readBack = store.ReadAll();

        var listed = (IList<ResearchEvent>)snapshot;
        Assert.ThrowsExactly<NotSupportedException>(() => listed.Add(readBack[0]));

        Assert.IsFalse(readBack is ResearchEvent[]);
        var readBackList = (IList<ResearchEvent>)readBack;
        Assert.ThrowsExactly<NotSupportedException>(() => readBackList[0] = first);

        Assert.AreEqual(0, snapshot.Count);
        Assert.AreEqual(2, readBack.Count);
    }

    [TestMethod]
    public void Append_order_preserved()
    {
        var store = new InMemoryProvenanceStore();
        var first = CreateEventRecord(Guid.Parse("00000000-0000-0000-0000-000000000005"), "protocol-approved", "first-subject");
        var second = CreateEventRecord(Guid.Parse("00000000-0000-0000-0000-000000000006"), "protocol-approved", "second-subject");

        store.Append(first);
        store.Append(second);
        var events = store.ReadAll();

        Assert.AreEqual("first-subject", events[0].Subject.EntityId);
        Assert.AreEqual("second-subject", events[1].Subject.EntityId);
    }

    [TestMethod]
    public void Event_content_cannot_be_mutated_after_append()
    {
        var store = new InMemoryProvenanceStore();
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();
        var mutableInputs = new List<ProvenanceEntityRef>
        {
            new("artifact", "before", ContentDigest.Sha256Utf8("original"))
        };
        var mutableOutputs = new List<ProvenanceEntityRef>
        {
            new("artifact", "output-before", ContentDigest.Sha256Utf8("output-original"))
        };

        var record = ResearchEventFactory.Create(
            ids,
            clock,
            new ProvenanceActivity("artifact-generated", "Artifact generated", false, false, false),
            new ProvenanceEntityRef("artifact", "artifact-1"),
            new ProvenanceAgent("automation-1", "automation"),
            inputs: mutableInputs,
            outputs: mutableOutputs);

        store.Append(record);
        mutableInputs.Clear();
        mutableInputs.Add(new ProvenanceEntityRef("artifact", "after", ContentDigest.Sha256Utf8("mutated")));
        mutableOutputs.Clear();
        mutableOutputs.Add(new ProvenanceEntityRef("artifact", "output-after", ContentDigest.Sha256Utf8("output-mutated")));

        var read = store.ReadAll()[0];
        var storedDigest = read.EventDigest;

        Assert.AreEqual(1, read.Inputs.Count);
        Assert.AreEqual("before", read.Inputs[0].EntityId);
        Assert.AreEqual(1, read.Outputs.Count);
        Assert.AreEqual("output-before", read.Outputs[0].EntityId);
        Assert.AreEqual(storedDigest, read.ToDigestEnvelope().ComputeDigest());
        Assert.IsFalse(read.Inputs is ProvenanceEntityRef[]);
        Assert.IsFalse(read.Outputs is ProvenanceEntityRef[]);

        var exposedInputs = (IList<ProvenanceEntityRef>)read.Inputs;
        var exposedOutputs = (IList<ProvenanceEntityRef>)read.Outputs;
        Assert.ThrowsExactly<NotSupportedException>(() =>
            exposedInputs[0] = new ProvenanceEntityRef("artifact", "after", ContentDigest.Sha256Utf8("mutated")));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            exposedOutputs[0] = new ProvenanceEntityRef("artifact", "output-after", ContentDigest.Sha256Utf8("output-mutated")));
    }

    [TestMethod]
    public void Raw_projection_content_is_not_canonical_provenance()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();

        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() =>
            ResearchEventFactory.Create(
                ids,
                clock,
                new ProvenanceActivity("cache-refreshed", "Cache refreshed", false, false, false),
                new ProvenanceEntityRef("cache", "cache-1"),
                new ProvenanceAgent("automation-1", "automation")));

        Assert.AreEqual(ProvenanceErrorCodes.ProjectionNotCanonical, error.Category);
    }

    private static ResearchEvent CreateEventRecord(Guid eventId, string subjectType, string subjectId = "subject-1")
    {
        return ResearchEventFactory.Create(
            new FixedIdGenerator(eventId),
            new FixedClock(),
            new ProvenanceActivity(subjectType, subjectType, false, false, false),
            new ProvenanceEntityRef(subjectType, subjectId),
            new ProvenanceAgent("researcher-1", "human"));
    }

    private static ResearchEvent CreateEventRecord(IIdGenerator ids, IClock clock, ProvenanceActivity activity)
    {
        return ResearchEventFactory.Create(
            ids,
            clock,
            activity,
            new ProvenanceEntityRef("protocol-version", "protocol-version-1"),
            new ProvenanceAgent("researcher-1", "human"));
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly Queue<Guid> _ids;

        public FixedIdGenerator(params Guid[] ids)
        {
            _ids = new Queue<Guid>(ids);
        }

        public Guid NewId()
        {
            if (_ids.Count == 0)
            {
                return Guid.NewGuid();
            }

            return _ids.Dequeue();
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
    }
}
