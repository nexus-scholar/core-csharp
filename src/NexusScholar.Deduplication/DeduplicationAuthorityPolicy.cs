using System.Globalization;
using System.Linq;
using System.Text;
using NexusScholar.Kernel;

namespace NexusScholar.Deduplication;

public static class DeduplicationAuthorityPolicyErrorCodes
{
    public const string DuplicateAuthorityMaterial = "duplicate-deduplication-authority-material";
    public const string InvalidAuthorityPolicy = "invalid-deduplication-authority-policy";
    public const string NonCanonicalAuthorityMaterial = "non-canonical-deduplication-authority-policy";
    public const string StaleAuthoritySourceBinding = "stale-deduplication-authority-source-binding";
    public const string UnauthorizedAuthorityActor = "unauthorized-deduplication-authority-actor";
    public const string UnsupportedAuthorityAction = "unsupported-deduplication-authority-action";
    public const string UnsupportedReasonCode = "unsupported-deduplication-reason";
}

public static class DeduplicationAuthorityPolicyConstants
{
    public const string SchemaId = "nexus.deduplication.authority-policy";
    public const string SchemaVersion = "1.0.0";
    public const string LocalAuthoritySourceKind = "local-deduplication-authority-policy";
    public const string HumanSubjectKind = "human";

    public const string MergeAction = "merge";
    public const string KeepSeparateAction = "keep-separate";
    public const string MarkUnresolvedAction = "mark-unresolved";

    public static IReadOnlyList<string> ClosedActions { get; } =
        Array.AsReadOnly(new[] { MergeAction, KeepSeparateAction, MarkUnresolvedAction });
}

public sealed record DeduplicationAuthorityPolicyActorRole(
    string ActorId,
    string Role,
    string SubjectKind = DeduplicationAuthorityPolicyConstants.HumanSubjectKind);

public sealed record DeduplicationAuthorityPolicyReasonGroup(string Action, IReadOnlyList<string> ReasonCodes);

public sealed record UnverifiedDeduplicationAuthorityPolicy(
    string SchemaId,
    string SchemaVersion,
    string AuthoritySourceKind,
    string PolicyId,
    string PolicyVersion,
    IReadOnlyList<DeduplicationAuthorityPolicyActorRole> AuthorizedActorRoles,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup> ReasonCodesByAction,
    bool RequiresRationale,
    string IssuedByActorId,
    string IssuedByRole,
    DateTimeOffset IssuedAt,
    string? SupersedesPolicyId = null,
    ContentDigest? SupersedesPolicyDigest = null,
    ContentDigest? PolicyDigest = null);

public sealed class VerifiedDeduplicationAuthorityPolicy
{
    internal VerifiedDeduplicationAuthorityPolicy(
        string policyId,
        string policyVersion,
        IReadOnlyList<DeduplicationAuthorityPolicyActorRole> authorizedActorRoles,
        IReadOnlyList<string> allowedActions,
        IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup> reasonCodesByAction,
        bool requiresRationale,
        string issuedByActorId,
        string issuedByRole,
        DateTimeOffset issuedAt,
        string? supersedesPolicyId,
        ContentDigest? supersedesPolicyDigest,
        ContentDigest policyDigest,
        DigestEnvelope policyDigestEnvelope)
    {
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        AuthorizedActorRoles = authorizedActorRoles;
        AllowedActions = allowedActions;
        ReasonCodesByAction = reasonCodesByAction;
        RequiresRationale = requiresRationale;
        IssuedByActorId = issuedByActorId;
        IssuedByRole = issuedByRole;
        IssuedAt = issuedAt;
        SupersedesPolicyId = supersedesPolicyId;
        SupersedesPolicyDigest = supersedesPolicyDigest;
        PolicyDigest = policyDigest;
        PolicyDigestEnvelope = policyDigestEnvelope;
    }

    public string PolicyId { get; }

    public string PolicyVersion { get; }

    public IReadOnlyList<DeduplicationAuthorityPolicyActorRole> AuthorizedActorRoles { get; }

    public IReadOnlyList<string> AllowedActions { get; }

    public IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup> ReasonCodesByAction { get; }

    public bool RequiresRationale { get; }

    public string IssuedByActorId { get; }

    public string IssuedByRole { get; }

    public DateTimeOffset IssuedAt { get; }

    public string? SupersedesPolicyId { get; }

    public ContentDigest? SupersedesPolicyDigest { get; }

    public ContentDigest PolicyDigest { get; }

    public DigestEnvelope PolicyDigestEnvelope { get; }

    public bool ContainsAuthorizedActor(string actorId, string role) =>
        AuthorizedActorRoles.Any(item => string.Equals(item.ActorId, actorId, StringComparison.Ordinal) &&
            string.Equals(item.Role, role, StringComparison.Ordinal));

    public IReadOnlyList<string> ReasonCodesForAction(string action) =>
        ReasonCodesByAction.SingleOrDefault(item => string.Equals(item.Action, action, StringComparison.Ordinal))
            ?.ReasonCodes
            ?? Array.Empty<string>();
}

public static class DeduplicationAuthorityPolicy
{
    public static VerifiedDeduplicationAuthorityPolicy CreatePolicyMaterial(UnverifiedDeduplicationAuthorityPolicy input)
    {
        var normalized = NormalizePolicy(input, requireCanonicalText: false);
        var canonical = BuildPolicyContent(normalized, canonicalizeCollections: true);
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            DeduplicationAuthorityPolicyConstants.SchemaId,
            DeduplicationAuthorityPolicyConstants.SchemaVersion,
            canonical);
        var policyDigest = envelope.ComputeDigest();

        return new VerifiedDeduplicationAuthorityPolicy(
            normalized.PolicyId,
            normalized.PolicyVersion,
            normalized.CanonicalActorRoles,
            normalized.CanonicalAllowedActions,
            normalized.CanonicalReasonCodes,
            normalized.RequiresRationale,
            normalized.IssuedByActorId,
            normalized.IssuedByRole,
            normalized.IssuedAt,
            normalized.SupersedesPolicyId,
            normalized.SupersedesPolicyDigest,
            policyDigest,
            envelope);
    }

    public static VerifiedDeduplicationAuthorityPolicy RehydratePolicyMaterial(UnverifiedDeduplicationAuthorityPolicy input)
    {
        EnsureKnownSchema(input);
        var normalized = NormalizePolicy(input, requireCanonicalText: true);
        var canonical = BuildPolicyContent(normalized, canonicalizeCollections: true);
        var provided = BuildPolicyContent(normalized, canonicalizeCollections: false);
        EnsureCanonicalInput("policy", provided, canonical);

        var policyDigest = input.PolicyDigest ?? default;
        if (!policyDigest.IsValid)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.StaleAuthoritySourceBinding,
                "Policy digest is required for persisted authority material.");
        }

        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            DeduplicationAuthorityPolicyConstants.SchemaId,
            DeduplicationAuthorityPolicyConstants.SchemaVersion,
            canonical);
        var computed = envelope.ComputeDigest();
        if (computed != policyDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy,
                "Policy digest does not match persisted authority material.");
        }

        return new VerifiedDeduplicationAuthorityPolicy(
            normalized.PolicyId,
            normalized.PolicyVersion,
            normalized.CanonicalActorRoles,
            normalized.CanonicalAllowedActions,
            normalized.CanonicalReasonCodes,
            normalized.RequiresRationale,
            normalized.IssuedByActorId,
            normalized.IssuedByRole,
            normalized.IssuedAt,
            normalized.SupersedesPolicyId,
            normalized.SupersedesPolicyDigest,
            policyDigest,
            envelope);
    }

    private static NormalizedDeduplicationAuthorityPolicy NormalizePolicy(UnverifiedDeduplicationAuthorityPolicy input, bool requireCanonicalText)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureKnownSchema(input);

        var policyId = RequireCanonicalText(input.PolicyId, nameof(input.PolicyId), requireCanonicalText);
        var policyVersion = RequireCanonicalText(input.PolicyVersion, nameof(input.PolicyVersion), requireCanonicalText);
        var issuedAt = RequireUtc(input.IssuedAt, nameof(input.IssuedAt));
        var issuedByActorId = RequireCanonicalText(input.IssuedByActorId, nameof(input.IssuedByActorId), requireCanonicalText);
        var issuedByRole = RequireCanonicalText(input.IssuedByRole, nameof(input.IssuedByRole), requireCanonicalText);
        var actorRoles = NormalizeActorRolePairs(input.AuthorizedActorRoles, nameof(input.AuthorizedActorRoles), requireCanonicalText);

        if (!actorRoles.Any(item => string.Equals(item.ActorId, issuedByActorId, StringComparison.Ordinal) &&
            string.Equals(item.Role, issuedByRole, StringComparison.Ordinal)))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.UnauthorizedAuthorityActor,
                "Policy issuer must be listed as an authorized actor-role pair.");
        }

        var allowedActions = (input.AllowedActions ?? throw InvalidPolicy("Allowed actions are required."))
            .Select(item => RequireCanonicalText(item, "allowed action", requireCanonicalText))
            .ToArray();

        var canonicalAllowedActions = DeduplicationAuthorityPolicyConstants.ClosedActions
            .Where(action => allowedActions.Contains(action, StringComparer.Ordinal))
            .ToArray();
        if (allowedActions.Length == 0 ||
            allowedActions.Distinct(StringComparer.Ordinal).Count() != allowedActions.Length ||
            !allowedActions.SequenceEqual(canonicalAllowedActions, StringComparer.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.UnsupportedAuthorityAction,
                "Policy actions must be a non-empty unique subset in canonical FE-01 order: merge, keep-separate, mark-unresolved.");
        }

        var reasonGroupInputs = input.ReasonCodesByAction is null
            ? throw InvalidPolicy("Reason-code groups are required.")
            : input.ReasonCodesByAction.ToArray();
        if (reasonGroupInputs.Any(group => group is null))
        {
            throw InvalidPolicy("Reason-code groups cannot contain null entries.");
        }

        if (reasonGroupInputs.Length != allowedActions.Length ||
            reasonGroupInputs.Select(item => RequireCanonicalText(item.Action, "action", requireCanonicalText)).ToArray()
                .Distinct(StringComparer.Ordinal).Count() != reasonGroupInputs.Length ||
            !reasonGroupInputs.Select(item => item.Action).SequenceEqual(allowedActions, StringComparer.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy,
                "Policy reason-code groups must match actions exactly in canonical order.");
        }

        var groupedReasons = reasonGroupInputs.Select(group => new DeduplicationAuthorityPolicyReasonGroup(
            RequireCanonicalText(group.Action, "action", requireCanonicalText),
            (group.ReasonCodes ?? throw InvalidPolicy("Reason codes are required.")).Select(
                item => RequireCanonicalText(item, "reason code", requireCanonicalText)).ToArray())).ToArray();

        foreach (var reasonCodes in groupedReasons.Select(item => item.ReasonCodes))
        {
            if (reasonCodes.Distinct(StringComparer.Ordinal).Count() != reasonCodes.Count())
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy,
                    "Reason codes for each action must be unique.");
            }
        }

        var supersedesPolicyId = input.SupersedesPolicyId;
        var hasSupersedesId = !string.IsNullOrWhiteSpace(supersedesPolicyId);
        var hasSupersedesDigest = input.SupersedesPolicyDigest is { IsValid: true };
        if (hasSupersedesId != hasSupersedesDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.StaleAuthoritySourceBinding,
                "Policy supersession must include both policy id and policy digest.");
        }

        var canonicalSupersedesId = hasSupersedesId
            ? RequireCanonicalText(supersedesPolicyId!, nameof(input.SupersedesPolicyId), requireCanonicalText)
            : null;
        var canonicalSupersedesDigest = hasSupersedesDigest ? input.SupersedesPolicyDigest : null;

        if (canonicalSupersedesId is not null && string.Equals(canonicalSupersedesId, policyId, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy,
                "A policy cannot supersede itself.");
        }

        return new NormalizedDeduplicationAuthorityPolicy(
            policyId,
            policyVersion,
            actorRoles,
            allowedActions,
            groupedReasons,
            input.RequiresRationale,
            issuedByActorId,
            issuedByRole,
            issuedAt,
            canonicalSupersedesId,
            canonicalSupersedesDigest);
    }

    private static IReadOnlyList<DeduplicationAuthorityPolicyActorRole> NormalizeActorRolePairs(
        IReadOnlyList<DeduplicationAuthorityPolicyActorRole> actorRoles,
        string name,
        bool requireCanonicalText)
    {
        var actorRoleItems = actorRoles ?? throw InvalidPolicy($"{name} is required.");
        if (actorRoleItems.Any(item => item is null))
        {
            throw InvalidPolicy("Authorized actor-role pairs cannot contain null entries.");
        }

        var normalized = actorRoleItems.Select(item =>
            new DeduplicationAuthorityPolicyActorRole(
                RequireCanonicalText(item.ActorId, nameof(item.ActorId), requireCanonicalText),
                RequireCanonicalText(item.Role, nameof(item.Role), requireCanonicalText),
                RequireCanonicalText(item.SubjectKind, nameof(item.SubjectKind), requireCanonicalText))).ToArray();

        if (normalized.Distinct(new ActorRoleComparer()).Count() != normalized.Length)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.DuplicateAuthorityMaterial,
                "Authorized actor-role pairs must be unique.");
        }

        foreach (var actorRole in normalized)
        {
            if (!string.Equals(actorRole.SubjectKind, DeduplicationAuthorityPolicyConstants.HumanSubjectKind, StringComparison.Ordinal))
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationAuthorityPolicyErrorCodes.UnauthorizedAuthorityActor,
                    "Authorized actor-role pairs must explicitly use subject kind 'human'.");
            }
        }

        return Array.AsReadOnly(normalized.ToArray());
    }

    private static CanonicalJsonObject BuildPolicyContent(NormalizedDeduplicationAuthorityPolicy normalized, bool canonicalizeCollections)
    {
        var canonicalActors = canonicalizeCollections
            ? normalized.AuthorizedActorRoles
                .OrderBy(item => item.ActorId, StringComparer.Ordinal)
                .ThenBy(item => item.Role, StringComparer.Ordinal)
                .ToArray()
            : normalized.AuthorizedActorRoles.ToArray();

        var canonicalReasonGroups = canonicalizeCollections
            ? normalized.CanonicalAllowedActions
                .Select(action => normalized.ReasonCodesByAction.Single(group =>
                    string.Equals(group.Action, action, StringComparison.Ordinal)))
                .Select(group => new DeduplicationAuthorityPolicyReasonGroup(
                    group.Action,
                    Array.AsReadOnly(group.ReasonCodes.OrderBy(item => item, StringComparer.Ordinal).ToArray())))
                .ToArray()
            : normalized.ReasonCodesByAction.ToArray();

        var builder = new CanonicalJsonObject()
            .Add("policy_id", normalized.PolicyId)
            .Add("schema_id", DeduplicationAuthorityPolicyConstants.SchemaId)
            .Add("schema_version", DeduplicationAuthorityPolicyConstants.SchemaVersion)
            .Add("policy_version", normalized.PolicyVersion)
            .Add("authority_source_kind", DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind)
            .Add("authorized_actor_roles", CanonicalJsonValue.Array(canonicalActors.Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                .Add("actor_id", item.ActorId)
                .Add("role", item.Role)
                .Add("subject_kind", item.SubjectKind)).ToArray()))
            .Add("allowed_actions", CanonicalJsonValue.Array(normalized.CanonicalAllowedActions.Select(CanonicalJsonValue.From).ToArray()))
            .Add("reason_codes_by_action", CanonicalJsonValue.Array(
                canonicalReasonGroups.Select(group => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("action", group.Action)
                    .Add("reason_codes", CanonicalJsonValue.Array(group.ReasonCodes.Select(CanonicalJsonValue.From).ToArray())))
                .ToArray()))
            .Add("requires_rationale", normalized.RequiresRationale)
            .Add("issued_by_actor_id", normalized.IssuedByActorId)
            .Add("issued_by_role", normalized.IssuedByRole)
            .AddTimestamp("issued_at", normalized.IssuedAt);

        if (normalized.SupersedesPolicyId is not null)
        {
            builder = builder
                .Add("supersedes_policy_id", normalized.SupersedesPolicyId)
                .Add("supersedes_policy_digest", normalized.SupersedesPolicyDigest!.Value.ToString());
        }

        return builder;
    }

    private static void EnsureCanonicalInput(string label, CanonicalJsonValue provided, CanonicalJsonValue canonical)
    {
        if (!string.Equals(Canonicalize(provided), Canonicalize(canonical), StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.NonCanonicalAuthorityMaterial,
                $"{label} authority material is not in canonical collection order.");
        }
    }

    private static string Canonicalize(CanonicalJsonValue value) => CanonicalJsonSerializer.Serialize(value);

    private static string RequireCanonicalText(string value, string name, bool enforceNormalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidPolicy($"{name} is required.");
        }

        var canonical = value;
        if (enforceNormalized && !canonical.IsNormalized(NormalizationForm.FormC))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, $"{name} must be NFC-normalized.");
        }

        return canonical;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy,
                $"{name} must be canonical UTC.");
        }

        return value;
    }

    private sealed class ActorRoleComparer : IEqualityComparer<DeduplicationAuthorityPolicyActorRole>
    {
        public bool Equals(DeduplicationAuthorityPolicyActorRole? x, DeduplicationAuthorityPolicyActorRole? y) =>
            string.Equals(x?.ActorId, y?.ActorId, StringComparison.Ordinal) &&
            string.Equals(x?.Role, y?.Role, StringComparison.Ordinal);

        public int GetHashCode(DeduplicationAuthorityPolicyActorRole obj) => HashCode.Combine(obj.ActorId, obj.Role);
    }

    private sealed class NormalizedDeduplicationAuthorityPolicy(
        string policyId,
        string policyVersion,
        IReadOnlyList<DeduplicationAuthorityPolicyActorRole> authorizedActorRoles,
        IReadOnlyList<string> allowedActions,
        IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup> reasonCodesByAction,
        bool requiresRationale,
        string issuedByActorId,
        string issuedByRole,
        DateTimeOffset issuedAt,
        string? supersedesPolicyId,
        ContentDigest? supersedesPolicyDigest)
    {
        public string PolicyId { get; } = policyId;
        public string PolicyVersion { get; } = policyVersion;
        public IReadOnlyList<DeduplicationAuthorityPolicyActorRole> AuthorizedActorRoles { get; } = Array.AsReadOnly(authorizedActorRoles.ToArray());
        public IReadOnlyList<string> AllowedActions { get; } = Array.AsReadOnly(allowedActions.ToArray());
        public IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup> ReasonCodesByAction { get; } = Array.AsReadOnly(reasonCodesByAction.ToArray());
        public bool RequiresRationale { get; } = requiresRationale;
        public string IssuedByActorId { get; } = issuedByActorId;
        public string IssuedByRole { get; } = issuedByRole;
        public DateTimeOffset IssuedAt { get; } = issuedAt;
        public string? SupersedesPolicyId { get; } = supersedesPolicyId;
        public ContentDigest? SupersedesPolicyDigest { get; } = supersedesPolicyDigest;
        public IReadOnlyList<DeduplicationAuthorityPolicyActorRole> CanonicalActorRoles
        {
            get
            {
                return Array.AsReadOnly(AuthorizedActorRoles
                    .OrderBy(item => item.ActorId, StringComparer.Ordinal)
                    .ThenBy(item => item.Role, StringComparer.Ordinal)
                    .ToArray());
            }
        }

        public IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup> CanonicalReasonCodes
        {
            get
            {
                return Array.AsReadOnly(CanonicalAllowedActions
                    .Select(action => ReasonCodesByAction.Single(item => string.Equals(item.Action, action, StringComparison.Ordinal)))
                    .Select(item => new DeduplicationAuthorityPolicyReasonGroup(item.Action,
                        Array.AsReadOnly(item.ReasonCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray())))
                    .ToArray());
            }
        }

        public IReadOnlyList<string> CanonicalAllowedActions => Array.AsReadOnly(AllowedActions.ToArray());
    }

    private static void EnsureKnownSchema(UnverifiedDeduplicationAuthorityPolicy input)
    {
        if (!string.Equals(input.SchemaId, DeduplicationAuthorityPolicyConstants.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(input.SchemaVersion, DeduplicationAuthorityPolicyConstants.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(input.AuthoritySourceKind, DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, StringComparison.Ordinal))
        {
            throw InvalidPolicy("Policy schema or authority-source kind is invalid.");
        }
    }

    private static DeduplicationAuthorityException InvalidPolicy(string message) =>
        new(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, message);
}
