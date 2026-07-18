using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    [TestMethod]
    public void Research_event_has_no_public_constructor()
    {
        Assert.AreEqual(0, typeof(ResearchEvent).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [TestMethod]
    public void Default_timestamp_is_rejected_during_event_construction_and_replay()
    {
        var constructionError = Assert.ThrowsExactly<ProvenanceRuleException>(() =>
            CreateEventRecord(
                new FixedIdGenerator(Guid.Parse("00000000-0000-0000-0000-000000000019")),
                new DefaultClock(),
                new ProvenanceActivity("protocol-approved", "Protocol approved", false, false, false)));
        Assert.AreEqual(ProvenanceErrorCodes.InvalidTimestamp, constructionError.Category);

        var valid = CreateEventRecord(
            Guid.Parse("00000000-0000-0000-0000-000000000020"),
            "protocol-approved");
        var persistedWithDefaultTimestamp = CloneEvent(valid, occurredAt: default(DateTimeOffset));
        var replayError = Assert.ThrowsExactly<ProvenanceRuleException>(() =>
            new InMemoryProvenanceStore().Append(persistedWithDefaultTimestamp));
        Assert.AreEqual(ProvenanceErrorCodes.InvalidTimestamp, replayError.Category);
    }

    [TestMethod]
    [DataRow("digest", ProvenanceErrorCodes.StaleEventDigest)]
    [DataRow("agent-id", ProvenanceErrorCodes.InvalidAgent)]
    [DataRow("agent-kind", ProvenanceErrorCodes.InvalidAgent)]
    [DataRow("timestamp", ProvenanceErrorCodes.InvalidTimestamp)]
    [DataRow("event-id", ProvenanceErrorCodes.InvalidEventId)]
    [DataRow("entity-kind", ProvenanceErrorCodes.ProjectionNotCanonical)]
    [DataRow("protocol-binding", ProvenanceErrorCodes.InvalidBinding)]
    [DataRow("workflow-binding", ProvenanceErrorCodes.InvalidBinding)]
    public void Append_rejects_forged_event_state(string mutation, string expectedCategory)
    {
        var valid = CreateEventRecord(Guid.Parse("00000000-0000-0000-0000-000000000020"), "protocol-approved");
        var forged = CloneEvent(
            valid,
            eventId: mutation == "event-id" ? default : valid.EventId,
            agent: mutation switch
            {
                "agent-id" => new ProvenanceAgent(string.Empty, ProvenanceAgent.HumanKind),
                "agent-kind" => new ProvenanceAgent("agent-1", "unknown"),
                _ => valid.Agent
            },
            occurredAt: mutation == "timestamp" ? valid.OccurredAt.ToOffset(TimeSpan.FromHours(1)) : valid.OccurredAt,
            subject: mutation == "entity-kind" ? new ProvenanceEntityRef("cache", "cache-1") : valid.Subject,
            eventDigest: mutation == "digest" ? ContentDigest.Sha256Utf8("forged") : valid.EventDigest,
            protocolBinding: mutation == "protocol-binding"
                ? new ProvenanceProtocolBinding("protocol-1", "version-1", 0, default)
                : valid.ProtocolBinding,
            workflowBinding: mutation == "workflow-binding"
                ? new ProvenanceWorkflowBinding("workflow-1", default, " ")
                : valid.WorkflowBinding);

        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() => new InMemoryProvenanceStore().Append(forged));
        Assert.AreEqual(expectedCategory, error.Category);
    }

    [TestMethod]
    public async Task Concurrent_distinct_event_appends_are_lossless()
    {
        var store = new InMemoryProvenanceStore();
        var events = Enumerable.Range(1, 64)
            .Select(value => CreateEventRecord(new Guid(value, 0, 0, new byte[8]), "protocol-approved", $"subject-{value:D2}"))
            .ToArray();

        await Task.WhenAll(events.Select(record => Task.Run(() => store.Append(record))));

        Assert.AreEqual(events.Length, store.ReadAll().Count);
        CollectionAssert.AreEquivalent(
            events.Select(item => item.EventId.ToString()).ToArray(),
            store.ReadAll().Select(item => item.EventId.ToString()).ToArray());
    }

    [TestMethod]
    public async Task Concurrent_duplicate_event_appends_accept_exactly_one()
    {
        var store = new InMemoryProvenanceStore();
        var record = CreateEventRecord(Guid.Parse("00000000-0000-0000-0000-000000000030"), "protocol-approved");
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            try
            {
                store.Append(record);
                return "accepted";
            }
            catch (ProvenanceRuleException error) when (error.Category == ProvenanceErrorCodes.DuplicateEventId)
            {
                return "duplicate";
            }
        })));

        Assert.AreEqual(1, results.Count(value => value == "accepted"));
        Assert.AreEqual(31, results.Count(value => value == "duplicate"));
        Assert.AreEqual(1, store.ReadAll().Count);
    }

    private static ResearchEvent CloneEvent(
        ResearchEvent source,
        EntityId<ProvenanceEventTag>? eventId = null,
        ProvenanceAgent? agent = null,
        DateTimeOffset? occurredAt = null,
        ProvenanceEntityRef? subject = null,
        ContentDigest? eventDigest = null,
        ProvenanceProtocolBinding? protocolBinding = null,
        ProvenanceWorkflowBinding? workflowBinding = null)
    {
        var constructor = typeof(ResearchEvent).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        return (ResearchEvent)constructor.Invoke(new object?[]
        {
            eventId ?? source.EventId,
            agent ?? source.Agent,
            source.Activity,
            occurredAt ?? source.OccurredAt,
            subject ?? source.Subject,
            source.Inputs,
            source.Outputs,
            eventDigest ?? source.EventDigest,
            protocolBinding ?? source.ProtocolBinding,
            workflowBinding ?? source.WorkflowBinding
        });
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

    private sealed class DefaultClock : IClock
    {
        public DateTimeOffset UtcNow => default;
    }
}
