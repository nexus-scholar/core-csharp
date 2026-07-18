using System.Globalization;
using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public static class ProtocolAuthorityPackageCanonicalCodec
{
    private const string SchemaId = "nexus.protocol-authority-package";
    private const string SchemaVersion = "1.0.0";

    public static byte[] Serialize(VerifiedProtocolVersion authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        var version = authority.Version;
        var protocol = CanonicalJsonValue.DeepClone(version.ToProtocolContentDigestEnvelope().Content) as CanonicalJsonObject
            ?? throw Invalid("Protocol content must be a canonical object.");
        protocol
            .Add("approval_ids", CanonicalJsonValue.Array(version.ApprovalIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("approval_policy_id", version.ApprovalPolicyId)
            .AddTimestamp("approved_at", version.ApprovedAt!.Value)
            .Add("content_digest", version.ContentDigest.ToString())
            .Add("status", Status(version.Status));
        if (version.SupersededByVersionId is not null)
        {
            protocol.Add("superseded_by_version_id", version.SupersededByVersionId);
        }

        var root = new CanonicalJsonObject()
            .Add("approval_policy", authority.ApprovalPolicy.ToCanonicalJson())
            .Add("approvals", CanonicalJsonValue.Array(authority.Approvals
                .OrderBy(item => item.Approval.ApprovalId, StringComparer.Ordinal)
                .Select(item => (CanonicalJsonValue)item.Approval.ToCanonicalJson(includeDigest: true))
                .ToArray()))
            .Add("human_actor_ids", CanonicalJsonValue.Array(authority.Approvals
                .Select(item => item.Approval.ApprovedBy.ToString())
                .Concat(version.Decisions.Select(item => item.DecidedBy.ToString()))
                .Concat(version.UnresolvedDecisions.Select(item => item.CreatedBy.ToString()))
                .Concat(version.Waivers.Select(item => item.RequestedBy.ToString()))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .Select(CanonicalJsonValue.From)
                .ToArray()))
            .Add("protocol", protocol)
            .Add("schema_id", SchemaId)
            .Add("schema_version", SchemaVersion);
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(root);
    }

    public static VerifiedProtocolVersion Rehydrate(byte[] bytes, ContentDigest expectedRawDigest)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (!expectedRawDigest.IsValid || ContentDigest.Sha256(bytes) != expectedRawDigest)
        {
            throw Invalid("Protocol authority package raw digest does not match.");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(
                CanonicalJsonValue.FromJsonElement(document.RootElement));
            if (!bytes.SequenceEqual(canonical))
            {
                throw Invalid("Protocol authority package bytes are not canonical.");
            }

            var root = document.RootElement;
            Exact(root, ["approval_policy", "approvals", "human_actor_ids", "protocol", "schema_id", "schema_version"], []);
            Require(root, "schema_id", SchemaId);
            Require(root, "schema_version", SchemaVersion);
            var policy = ParsePolicy(root.GetProperty("approval_policy"));
            var protocolInput = ParseProtocol(root.GetProperty("protocol"), policy);
            if (protocolInput.Status is not ProtocolStatus.Approved and not ProtocolStatus.Superseded)
            {
                throw Invalid("Protocol authority package requires approved or superseded Protocol version.");
            }

            var candidate = Candidate(protocolInput, policy);
            var approvalInputs = root.GetProperty("approvals").EnumerateArray().Select(ParseApproval).ToArray();
            if (approvalInputs.Select(item => item.ApprovalId).Distinct(StringComparer.Ordinal).Count() != approvalInputs.Length)
            {
                throw Invalid("Protocol authority package contains duplicate approval records.");
            }

            var humanActors = Strings(root, "human_actor_ids").Select(ActorId.From).ToHashSet();
            if (humanActors.Count == 0)
            {
                throw Invalid("Protocol authority package requires an explicit human actor roster.");
            }
            var bootstrap = new Resolver(policy, humanActors, Array.Empty<VerifiedProtocolApproval>());
            var approvals = approvalInputs
                .Select(item => ProtocolRehydrator.RehydrateApproval(item, candidate, policy, bootstrap))
                .ToArray();
            var verified = ProtocolRehydrator.RehydrateVersion(
                protocolInput,
                new Resolver(policy, humanActors, approvals));
            if (!Serialize(verified).SequenceEqual(bytes))
            {
                throw Invalid("Protocol authority package does not reproduce from verified authority.");
            }

            return verified;
        }
        catch (ProtocolRuleException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            ArgumentException or FormatException or OverflowException)
        {
            throw Invalid($"Protocol authority package failed to rehydrate: {exception.Message}");
        }
    }

    private static ApprovalPolicy ParsePolicy(JsonElement value)
    {
        Exact(value, ["allows_automation", "minimum_approvals", "mode", "policy_id", "policy_version",
            "requires_distinct_actors", "required_roles"], ["method_pack_id", "custom_rule_id"]);
        return new ApprovalPolicy(
            Text(value, "policy_id"),
            Text(value, "policy_version"),
            ParsePolicyMode(Text(value, "mode")),
            Strings(value, "required_roles"),
            value.GetProperty("minimum_approvals").GetInt32(),
            value.GetProperty("requires_distinct_actors").GetBoolean(),
            value.GetProperty("allows_automation").GetBoolean(),
            OptionalText(value, "method_pack_id"),
            OptionalText(value, "custom_rule_id"));
    }

    private static UnverifiedProtocolVersion ParseProtocol(JsonElement value, ApprovalPolicy policy)
    {
        Exact(value, ["approval_ids", "approval_policy_id", "approved_at", "content_digest", "decisions",
            "intent", "project_id", "protocol_id", "required_decisions", "status", "template",
            "unresolved_decisions", "values", "version_id", "version_number", "waivers"],
            ["supersedes_version_id", "superseded_by_version_id", "amendment_id"]);
        var template = value.GetProperty("template");
        Exact(template, ["template_digest", "template_id", "template_version"], []);
        var intent = value.GetProperty("intent");
        Exact(intent, ["raw_subject", "review_goal"], ["selected_review_family"]);
        var values = CanonicalJsonValue.FromJsonElement(value.GetProperty("values")) as CanonicalJsonObject
            ?? throw Invalid("Protocol values must be an object.");
        return new UnverifiedProtocolVersion(
            Text(value, "version_id"),
            Text(value, "protocol_id"),
            Text(value, "project_id"),
            value.GetProperty("version_number").GetInt32(),
            ParseStatus(Text(value, "status")),
            new ProtocolTemplate(
                Text(template, "template_id"),
                Text(template, "template_version"),
                Digest(template, "template_digest")),
            new ProtocolIntent(
                Text(intent, "raw_subject"),
                Text(intent, "review_goal"),
                OptionalText(intent, "selected_review_family")),
            values,
            value.GetProperty("required_decisions").EnumerateArray().Select(ParseRequiredDecision).ToArray(),
            value.GetProperty("decisions").EnumerateArray().Select(ParseDecision).ToArray(),
            value.GetProperty("waivers").EnumerateArray().Select(ParseWaiver).ToArray(),
            value.GetProperty("unresolved_decisions").EnumerateArray().Select(ParseUnresolved).ToArray(),
            Digest(value, "content_digest"),
            policy,
            Strings(value, "approval_ids"),
            Timestamp(value, "approved_at"),
            OptionalText(value, "supersedes_version_id"),
            OptionalText(value, "superseded_by_version_id"),
            OptionalText(value, "amendment_id"));
    }

    private static RequiredDecisionDefinition ParseRequiredDecision(JsonElement value)
    {
        Exact(value, ["allows_unresolved", "approval_gate_id", "decision_key", "description",
            "required_before", "source_requirement_id", "title", "value_schema"], []);
        return new RequiredDecisionDefinition(
            Text(value, "decision_key"),
            Text(value, "title"),
            Text(value, "description"),
            CanonicalJsonValue.FromJsonElement(value.GetProperty("value_schema")),
            Text(value, "required_before"),
            Text(value, "approval_gate_id"),
            Text(value, "source_requirement_id"),
            value.GetProperty("allows_unresolved").GetBoolean());
    }

    private static ProtocolDecision ParseDecision(JsonElement value)
    {
        Exact(value, ["decided_at", "decided_by", "decision_id", "decision_key", "value"],
            ["rationale", "source_proposal_digest", "supersedes_decision_id"]);
        return new ProtocolDecision(
            Text(value, "decision_id"),
            Text(value, "decision_key"),
            CanonicalJsonValue.FromJsonElement(value.GetProperty("value")),
            OptionalText(value, "rationale"),
            ActorId.From(Text(value, "decided_by")),
            Timestamp(value, "decided_at"),
            OptionalDigest(value, "source_proposal_digest"),
            OptionalText(value, "supersedes_decision_id"));
    }

    private static ProtocolWaiver ParseWaiver(JsonElement value)
    {
        Exact(value, ["affected_requirement_id", "approval_ids", "approval_policy_id", "consequence_warning",
            "disclosure_mapping", "rationale", "requested_at", "requested_by", "waiver_id",
            ], ["condition", "expires_at"]);
        return new ProtocolWaiver(
            Text(value, "waiver_id"),
            Text(value, "affected_requirement_id"),
            OptionalText(value, "condition"),
            OptionalTimestamp(value, "expires_at"),
            Text(value, "rationale"),
            Text(value, "consequence_warning"),
            Text(value, "disclosure_mapping"),
            ActorId.From(Text(value, "requested_by")),
            Timestamp(value, "requested_at"),
            Text(value, "approval_policy_id"),
            Strings(value, "approval_ids"));
    }

    private static UnresolvedDecision ParseUnresolved(JsonElement value)
    {
        Exact(value, ["blocks_protocol_approval", "created_at", "created_by", "decision_key", "question",
            "reason", "required_before", "unresolved_id"], []);
        return new UnresolvedDecision(
            Text(value, "unresolved_id"),
            Text(value, "decision_key"),
            Text(value, "question"),
            Text(value, "reason"),
            Text(value, "required_before"),
            ActorId.From(Text(value, "created_by")),
            Timestamp(value, "created_at"),
            value.GetProperty("blocks_protocol_approval").GetBoolean());
    }

    private static UnverifiedProtocolApproval ParseApproval(JsonElement value)
    {
        Exact(value, ["approval_id", "approval_record_digest", "approved_at", "approved_by",
            "approved_by_is_human", "content_digest", "decision", "policy_id", "policy_mode",
            "policy_version", "protocol_id", "protocol_version_id", "protocol_version_number",
            "target_id", "target_type"], ["role", "rationale", "supersedes_approval_id"]);
        if (!value.GetProperty("approved_by_is_human").GetBoolean())
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.NonHumanApprovalActor,
                "Protocol authority package approval actor is not human.");
        }

        return new UnverifiedProtocolApproval(
            Text(value, "approval_id"),
            Text(value, "target_type"),
            Text(value, "target_id"),
            Text(value, "protocol_id"),
            Text(value, "protocol_version_id"),
            value.GetProperty("protocol_version_number").GetInt32(),
            Digest(value, "content_digest"),
            Text(value, "policy_id"),
            Text(value, "policy_version"),
            ParsePolicyMode(Text(value, "policy_mode")),
            ParseApprovalDecision(Text(value, "decision")),
            ActorId.From(Text(value, "approved_by")),
            Timestamp(value, "approved_at"),
            OptionalText(value, "role"),
            OptionalText(value, "rationale"),
            OptionalText(value, "supersedes_approval_id"),
            Digest(value, "approval_record_digest"));
    }

    private static ProtocolVersion Candidate(UnverifiedProtocolVersion input, ApprovalPolicy policy) => new(
        input.Id, input.ProtocolId, input.ProjectId, input.VersionNumber, ProtocolStatus.ReadyForReview,
        input.Template, input.Intent, input.Values, input.RequiredDecisions, input.Decisions, input.Waivers,
        input.ContentDigest, policy.PolicyId, Array.Empty<string>(), null,
        input.SupersedesVersionId, null, input.AmendmentId, input.UnresolvedDecisions);

    private static void Exact(JsonElement value, string[] required, string[] optional)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("Protocol authority package member must be an object.");
        }

        var names = value.EnumerateObject().Select(item => item.Name).ToArray();
        var allowed = required.Concat(optional).ToHashSet(StringComparer.Ordinal);
        if (!required.All(name => names.Contains(name, StringComparer.Ordinal)) ||
            names.Any(name => !allowed.Contains(name)))
        {
            throw Invalid("Protocol authority package has missing or unknown fields.");
        }
    }

    private static void Require(JsonElement value, string name, string expected)
    {
        if (!string.Equals(Text(value, name), expected, StringComparison.Ordinal))
        {
            throw Invalid($"Protocol authority package field '{name}' is invalid.");
        }
    }

    private static string Text(JsonElement value, string name) =>
        value.GetProperty(name).ValueKind == JsonValueKind.String
            ? value.GetProperty(name).GetString()!
            : throw Invalid($"Protocol authority package field '{name}' must be a string.");

    private static string? OptionalText(JsonElement value, string name) =>
        value.TryGetProperty(name, out var item) ? item.GetString() : null;

    private static IReadOnlyList<string> Strings(JsonElement value, string name) =>
        value.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static ContentDigest Digest(JsonElement value, string name) => ContentDigest.Parse(Text(value, name));
    private static ContentDigest? OptionalDigest(JsonElement value, string name) =>
        value.TryGetProperty(name, out _) ? Digest(value, name) : null;
    private static DateTimeOffset Timestamp(JsonElement value, string name)
    {
        var text = Text(value, name);
        CanonicalTimestamp.ValidateCanonicalUtc(text);
        return DateTimeOffset.ParseExact(
            text,
            CanonicalTimestamp.DefaultUtcFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
    private static DateTimeOffset? OptionalTimestamp(JsonElement value, string name) =>
        value.TryGetProperty(name, out _) ? Timestamp(value, name) : null;

    private static string Status(ProtocolStatus value) => value switch
    {
        ProtocolStatus.Approved => "approved",
        ProtocolStatus.Superseded => "superseded",
        ProtocolStatus.ReadyForReview => "ready_for_review",
        ProtocolStatus.Draft => "draft",
        ProtocolStatus.Withdrawn => "withdrawn",
        _ => throw Invalid("Protocol status is unsupported.")
    };

    private static ProtocolStatus ParseStatus(string value) => value switch
    {
        "approved" => ProtocolStatus.Approved,
        "superseded" => ProtocolStatus.Superseded,
        "ready_for_review" => ProtocolStatus.ReadyForReview,
        "draft" => ProtocolStatus.Draft,
        "withdrawn" => ProtocolStatus.Withdrawn,
        _ => throw Invalid("Protocol status is unsupported.")
    };

    private static ApprovalPolicyMode ParsePolicyMode(string value) => value switch
    {
        "single_researcher" => ApprovalPolicyMode.SingleResearcher,
        "dual_independent" => ApprovalPolicyMode.DualIndependent,
        "methodologist" => ApprovalPolicyMode.Methodologist,
        "information_specialist" => ApprovalPolicyMode.InformationSpecialist,
        "statistician" => ApprovalPolicyMode.Statistician,
        "project_owner" => ApprovalPolicyMode.ProjectOwner,
        "institutional_signoff" => ApprovalPolicyMode.InstitutionalSignoff,
        "custom_role_expression" => ApprovalPolicyMode.CustomRoleExpression,
        _ => throw Invalid("Protocol approval policy mode is unsupported.")
    };

    private static ProtocolApprovalDecision ParseApprovalDecision(string value) => value switch
    {
        "approved" => ProtocolApprovalDecision.Approved,
        "rejected" => ProtocolApprovalDecision.Rejected,
        "changes_requested" => ProtocolApprovalDecision.ChangesRequested,
        "withdrawn" => ProtocolApprovalDecision.Withdrawn,
        _ => throw Invalid("Protocol approval decision is unsupported.")
    };

    private static ProtocolRuleException Invalid(string message) =>
        new(ProtocolErrorCodes.InvalidAuthorityPackage, message);

    private sealed class Resolver(
        ApprovalPolicy policy,
        IEnumerable<ActorId> humanActors,
        IEnumerable<VerifiedProtocolApproval> approvals) : IProtocolAuthorityResolver
    {
        private readonly HashSet<ActorId> _humans = humanActors.ToHashSet();
        private readonly IReadOnlyDictionary<string, VerifiedProtocolApproval> _approvals =
            approvals.ToDictionary(item => item.Approval.ApprovalId, StringComparer.Ordinal);

        public ApprovalPolicy ResolveApprovalPolicy(ProtocolTemplate template) => policy;
        public bool IsHumanActor(ActorId actorId) => _humans.Contains(actorId);
        public VerifiedProtocolApproval ResolveApproval(string approvalId) =>
            _approvals.TryGetValue(approvalId, out var value) ? value : null!;
    }
}
