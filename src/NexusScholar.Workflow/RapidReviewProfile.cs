using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.Workflow;

public static class RapidReviewProfileConstants
{
    public const string SchemaId = "nexus.workflow-profile.rapid-review";
    public const string SchemaVersion = "1.0.0";
    public static IReadOnlyList<string> ProtectedInvariants { get; } = Array.AsReadOnly(new[]
    {
        "actor-identity", "append-only-history", "evidence-identity", "human-scientific-authority",
        "invalidation", "provenance", "snapshot-immutability"
    });
}

public static class RapidReviewProfileErrorCodes
{
    public const string InvalidProfile = "invalid-rapid-review-profile";
    public const string StaleAuthority = "stale-rapid-review-authority";
    public const string UnsafeShortcut = "unsafe-rapid-review-shortcut";
    public const string NonCanonicalRecord = "non-canonical-rapid-review-profile";
}

public sealed class RapidReviewProfileException : InvalidOperationException
{
    public RapidReviewProfileException(string category, string message) : base(message) => Category = category;
    public string Category { get; }
}

public sealed record RapidReviewShortcut(
    string ShortcutId,
    string ActivationInputRef,
    IReadOnlyList<string> AffectedRequirementRefs,
    IReadOnlyList<string> AffectedNodeRefs,
    string Consequence,
    string Mitigation,
    IReadOnlyList<string> RequiredMitigationArtifactRefs,
    string ApprovalRequirementRef,
    string ReportingDisclosure,
    string InvalidationPolicyRef);

public sealed record UnverifiedRapidReviewProfile(
    string SchemaId,
    string SchemaVersion,
    string ProfileId,
    string WorkflowId,
    ContentDigest WorkflowDigest,
    string TemplateId,
    string TemplateVersion,
    ContentDigest TemplateDigest,
    string ProtocolId,
    string ProtocolVersionId,
    ContentDigest ProtocolContentDigest,
    IReadOnlyList<RapidReviewShortcut> Shortcuts,
    IReadOnlyList<string> ProtectedInvariants,
    ContentDigest RecordDigest);

public sealed class VerifiedRapidReviewProfile
{
    internal VerifiedRapidReviewProfile(UnverifiedRapidReviewProfile record, DigestEnvelope envelope)
    {
        Record = record with
        {
            Shortcuts = Array.AsReadOnly(record.Shortcuts.Select(Clone).ToArray()),
            ProtectedInvariants = Array.AsReadOnly(record.ProtectedInvariants.ToArray())
        };
        DigestEnvelope = envelope;
    }

    public UnverifiedRapidReviewProfile Record { get; }
    public IReadOnlyList<RapidReviewShortcut> Shortcuts => Record.Shortcuts;
    public DigestEnvelope DigestEnvelope { get; }
    public ContentDigest RecordDigest => DigestEnvelope.ComputeDigest();
    public CanonicalJsonObject ToCanonicalJson() => DigestEnvelope.ToCanonicalJsonObject();

    private static RapidReviewShortcut Clone(RapidReviewShortcut item) => item with
    {
        AffectedRequirementRefs = Array.AsReadOnly(item.AffectedRequirementRefs.ToArray()),
        AffectedNodeRefs = Array.AsReadOnly(item.AffectedNodeRefs.ToArray()),
        RequiredMitigationArtifactRefs = Array.AsReadOnly(item.RequiredMitigationArtifactRefs.ToArray())
    };
}

public static class RapidReviewProfileAuthority
{
    public static VerifiedRapidReviewProfile Create(
        string profileId,
        VerifiedWorkflowDefinition workflow,
        IEnumerable<RapidReviewShortcut> shortcuts)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        var normalized = NormalizeShortcuts(shortcuts, workflow.ResolvedTemplate);
        var definition = workflow.Definition;
        var protocol = workflow.ProtocolAuthority.Version;
        var content = BuildContent(profileId, definition.WorkflowId, definition.WorkflowDigest,
            definition.TemplateId, definition.TemplateVersion, definition.TemplateDigest,
            protocol.ProtocolId, protocol.Id, protocol.ContentDigest, normalized, RapidReviewProfileConstants.ProtectedInvariants);
        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, RapidReviewProfileConstants.SchemaId,
            RapidReviewProfileConstants.SchemaVersion, content);
        var record = new UnverifiedRapidReviewProfile(RapidReviewProfileConstants.SchemaId, RapidReviewProfileConstants.SchemaVersion,
            Require(profileId, nameof(profileId)), definition.WorkflowId, definition.WorkflowDigest, definition.TemplateId,
            definition.TemplateVersion, definition.TemplateDigest, protocol.ProtocolId, protocol.Id, protocol.ContentDigest,
            normalized, RapidReviewProfileConstants.ProtectedInvariants, envelope.ComputeDigest());
        return new VerifiedRapidReviewProfile(record, envelope);
    }

    public static VerifiedRapidReviewProfile Rehydrate(UnverifiedRapidReviewProfile input, VerifiedWorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.SchemaId != RapidReviewProfileConstants.SchemaId || input.SchemaVersion != RapidReviewProfileConstants.SchemaVersion)
            throw Invalid(RapidReviewProfileErrorCodes.InvalidProfile, "Unknown Rapid Review profile schema.");
        var expected = Create(input.ProfileId, workflow, input.Shortcuts);
        if (input.WorkflowId != expected.Record.WorkflowId || input.WorkflowDigest != expected.Record.WorkflowDigest ||
            input.TemplateId != expected.Record.TemplateId || input.TemplateVersion != expected.Record.TemplateVersion ||
            input.TemplateDigest != expected.Record.TemplateDigest || input.ProtocolId != expected.Record.ProtocolId ||
            input.ProtocolVersionId != expected.Record.ProtocolVersionId || input.ProtocolContentDigest != expected.Record.ProtocolContentDigest)
            throw Invalid(RapidReviewProfileErrorCodes.StaleAuthority, "Rapid Review profile authority binding is stale.");
        if (!input.ProtectedInvariants.SequenceEqual(RapidReviewProfileConstants.ProtectedInvariants) || input.RecordDigest != expected.RecordDigest)
            throw Invalid(RapidReviewProfileErrorCodes.UnsafeShortcut, "Rapid Review protected invariants or digest are altered.");
        if (!CanonicalJsonSerializer.SerializeToUtf8Bytes(BuildContent(input.ProfileId, input.WorkflowId, input.WorkflowDigest,
                input.TemplateId, input.TemplateVersion, input.TemplateDigest, input.ProtocolId, input.ProtocolVersionId,
                input.ProtocolContentDigest, input.Shortcuts, input.ProtectedInvariants))
            .SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(expected.DigestEnvelope.Content)))
            throw Invalid(RapidReviewProfileErrorCodes.NonCanonicalRecord, "Rapid Review shortcuts are altered or not canonically ordered.");
        return expected;
    }

    private static IReadOnlyList<RapidReviewShortcut> NormalizeShortcuts(IEnumerable<RapidReviewShortcut> shortcuts, WorkflowTemplate template)
    {
        var items = (shortcuts ?? throw new ArgumentNullException(nameof(shortcuts))).Select(item => item ?? throw Invalid(
            RapidReviewProfileErrorCodes.InvalidProfile, "Rapid Review shortcut cannot be null.")).ToArray();
        if (items.Length == 0 || items.Select(item => Require(item.ShortcutId, nameof(item.ShortcutId))).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw Invalid(RapidReviewProfileErrorCodes.InvalidProfile, "Rapid Review profile requires unique shortcuts.");
        var inputById = template.RequiredInputs.ToDictionary(item => item.InputId, StringComparer.Ordinal);
        var nodeIds = template.Nodes.Select(item => item.NodeId).ToHashSet(StringComparer.Ordinal);
        var artifacts = template.ArtifactDeclarations.Select(item => item.ArtifactRef).ToHashSet(StringComparer.Ordinal);
        var approvals = template.ApprovalRequirements.ToDictionary(item => item.ApprovalRequirementId, StringComparer.Ordinal);
        var invalidations = template.InvalidationPolicies.ToDictionary(item => item.InvalidationPolicyId, StringComparer.Ordinal);
        return Array.AsReadOnly(items.Select(item =>
        {
            if (!inputById.TryGetValue(Require(item.ActivationInputRef, nameof(item.ActivationInputRef)), out var activation) ||
                activation.InputKind != WorkflowTemplateInputKind.ScientificConduct || activation.DefaultValue is not null)
                throw Invalid(RapidReviewProfileErrorCodes.UnsafeShortcut, "Shortcut activation must be a no-default scientific-conduct input.");
            var requirements = Set(item.AffectedRequirementRefs, "affected requirement");
            var nodes = Set(item.AffectedNodeRefs, "affected node");
            var mitigationArtifacts = Set(item.RequiredMitigationArtifactRefs, "mitigation artifact");
            if (requirements.Count == 0 || requirements.Any(id => !inputById.ContainsKey(id)) ||
                nodes.Count == 0 || nodes.Any(id => !nodeIds.Contains(id)) ||
                mitigationArtifacts.Count == 0 || mitigationArtifacts.Any(id => !artifacts.Contains(id)))
                throw Invalid(RapidReviewProfileErrorCodes.UnsafeShortcut, "Shortcut references unknown or incomplete requirements, nodes, or mitigation artifacts.");
            if (!approvals.TryGetValue(Require(item.ApprovalRequirementRef, nameof(item.ApprovalRequirementRef)), out var approval) ||
                approval.AllowsAutomation || approval.MinimumApprovals <= 0 || approval.RequiredRoles.Count == 0)
                throw Invalid(RapidReviewProfileErrorCodes.UnsafeShortcut, "Shortcut requires non-automated human approval.");
            if (!invalidations.TryGetValue(Require(item.InvalidationPolicyRef, nameof(item.InvalidationPolicyRef)), out var invalidation) ||
                string.IsNullOrWhiteSpace(invalidation.RequiredAction) ||
                requirements.Except(invalidation.AffectedRequirementRefs, StringComparer.Ordinal).Any() ||
                nodes.Except(invalidation.AffectedNodeRefs, StringComparer.Ordinal).Any())
                throw Invalid(RapidReviewProfileErrorCodes.UnsafeShortcut, "Shortcut invalidation policy does not cover its affected conduct.");
            return item with
            {
                ShortcutId = Require(item.ShortcutId, nameof(item.ShortcutId)),
                ActivationInputRef = activation.InputId,
                Consequence = Require(item.Consequence, nameof(item.Consequence)),
                Mitigation = Require(item.Mitigation, nameof(item.Mitigation)),
                ReportingDisclosure = Require(item.ReportingDisclosure, nameof(item.ReportingDisclosure)),
                ApprovalRequirementRef = approval.ApprovalRequirementId,
                InvalidationPolicyRef = invalidation.InvalidationPolicyId,
                AffectedRequirementRefs = requirements,
                AffectedNodeRefs = nodes,
                RequiredMitigationArtifactRefs = mitigationArtifacts
            };
        }).OrderBy(item => item.ShortcutId, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<string> Set(IEnumerable<string> values, string name)
    {
        var result = (values ?? throw new ArgumentNullException(name)).Select(value => Require(value, name)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Length)
            throw Invalid(RapidReviewProfileErrorCodes.InvalidProfile, $"Duplicate {name} reference.");
        return Array.AsReadOnly(result);
    }

    internal static CanonicalJsonObject BuildContent(string profileId, string workflowId, ContentDigest workflowDigest,
        string templateId, string templateVersion, ContentDigest templateDigest, string protocolId, string protocolVersionId,
        ContentDigest protocolContentDigest, IEnumerable<RapidReviewShortcut> shortcuts, IEnumerable<string> protectedInvariants) =>
        new CanonicalJsonObject().Add("profile_id", Require(profileId, nameof(profileId))).Add("workflow_id", workflowId)
            .Add("workflow_digest", workflowDigest.ToString()).Add("template_id", templateId).Add("template_version", templateVersion)
            .Add("template_digest", templateDigest.ToString()).Add("protocol_id", protocolId).Add("protocol_version_id", protocolVersionId)
            .Add("protocol_content_digest", protocolContentDigest.ToString()).Add("shortcuts", CanonicalJsonValue.Array(shortcuts.Select(ShortcutJson).ToArray()))
            .Add("protected_invariants", CanonicalJsonValue.Array(protectedInvariants.Select(CanonicalJsonValue.From).ToArray()));

    private static CanonicalJsonObject ShortcutJson(RapidReviewShortcut item) => new CanonicalJsonObject()
        .Add("shortcut_id", item.ShortcutId).Add("activation_input_ref", item.ActivationInputRef)
        .Add("affected_requirement_refs", TextArray(item.AffectedRequirementRefs)).Add("affected_node_refs", TextArray(item.AffectedNodeRefs))
        .Add("consequence", item.Consequence).Add("mitigation", item.Mitigation)
        .Add("required_mitigation_artifact_refs", TextArray(item.RequiredMitigationArtifactRefs))
        .Add("approval_requirement_ref", item.ApprovalRequirementRef).Add("reporting_disclosure", item.ReportingDisclosure)
        .Add("invalidation_policy_ref", item.InvalidationPolicyRef);
    private static CanonicalJsonValue TextArray(IEnumerable<string> values) => CanonicalJsonValue.Array(values.Select(CanonicalJsonValue.From).ToArray());
    private static string Require(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value.Trim() :
        throw Invalid(RapidReviewProfileErrorCodes.UnsafeShortcut, $"{name} is required.");
    private static RapidReviewProfileException Invalid(string category, string message) => new(category, message);
}

public static class RapidReviewProfileCanonicalCodec
{
    public static byte[] Serialize(VerifiedRapidReviewProfile profile) => CanonicalJsonSerializer.SerializeToUtf8Bytes(profile.ToCanonicalJson());

    public static VerifiedRapidReviewProfile Rehydrate(byte[] canonicalBytes, ContentDigest expectedDigest,
        UnverifiedRapidReviewProfile input, VerifiedWorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(canonicalBytes);
        var verified = RapidReviewProfileAuthority.Rehydrate(input, workflow);
        var expected = Serialize(verified);
        if (expectedDigest != verified.RecordDigest || !canonicalBytes.SequenceEqual(expected))
            throw new RapidReviewProfileException(RapidReviewProfileErrorCodes.NonCanonicalRecord,
                "Rapid Review profile bytes or expected digest do not match reconstructed authority.");
        using var document = JsonDocument.Parse(canonicalBytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new RapidReviewProfileException(RapidReviewProfileErrorCodes.NonCanonicalRecord, "Rapid Review profile must be a canonical object.");
        return verified;
    }
}
