using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public static class ProtocolSupplementalTargetTypes
{
    public const string Waiver = "protocol-waiver";
    public const string Amendment = "protocol-amendment";
    public const string Deviation = "protocol-deviation";
}

public sealed record UnverifiedProtocolSupplementalApproval(
    string ApprovalId,
    string TargetType,
    string TargetId,
    ContentDigest TargetDigest,
    string PolicyId,
    string PolicyVersion,
    ApprovalPolicyMode PolicyMode,
    ProtocolApprovalDecision Decision,
    ActorId ApprovedBy,
    DateTimeOffset ApprovedAt,
    string? Role,
    string? Rationale,
    string? SupersedesApprovalId,
    ContentDigest ApprovalRecordDigest);

public sealed record UnverifiedProtocolWaiver(ProtocolWaiver Waiver);

public sealed record UnverifiedProtocolAmendment(ProtocolAmendment Amendment);

public interface IProtocolSupplementalAuthorityResolver
{
    ApprovalPolicy ResolvePolicy(string targetType, string targetId);

    bool IsHumanActor(ActorId actorId);

    VerifiedProtocolSupplementalApproval ResolveApproval(string approvalId);

    VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId);
}

public sealed class ProtocolSupplementalApproval
{
    internal ProtocolSupplementalApproval(
        string approvalId,
        string targetType,
        string targetId,
        ContentDigest targetDigest,
        string policyId,
        string policyVersion,
        ApprovalPolicyMode policyMode,
        ProtocolApprovalDecision decision,
        ActorId approvedBy,
        DateTimeOffset approvedAt,
        string? role,
        string? rationale,
        string? supersedesApprovalId,
        ContentDigest approvalRecordDigest)
    {
        ApprovalId = Guard.NotBlank(approvalId, nameof(approvalId));
        TargetType = Guard.NotBlank(targetType, nameof(targetType));
        TargetId = Guard.NotBlank(targetId, nameof(targetId));
        TargetDigest = RequireDigest(targetDigest, nameof(targetDigest));
        PolicyId = Guard.NotBlank(policyId, nameof(policyId));
        PolicyVersion = Guard.NotBlank(policyVersion, nameof(policyVersion));
        PolicyMode = policyMode;
        Decision = decision;
        ApprovedBy = RequireActor(approvedBy);
        ApprovedAt = RequireUtc(approvedAt, nameof(approvedAt));
        Role = role;
        Rationale = rationale;
        SupersedesApprovalId = supersedesApprovalId;
        ApprovalRecordDigest = RequireDigest(approvalRecordDigest, nameof(approvalRecordDigest));
    }

    public string ApprovalId { get; }
    public string TargetType { get; }
    public string TargetId { get; }
    public ContentDigest TargetDigest { get; }
    public string PolicyId { get; }
    public string PolicyVersion { get; }
    public ApprovalPolicyMode PolicyMode { get; }
    public ProtocolApprovalDecision Decision { get; }
    public ActorId ApprovedBy { get; }
    public DateTimeOffset ApprovedAt { get; }
    public string? Role { get; }
    public string? Rationale { get; }
    public string? SupersedesApprovalId { get; }
    public ContentDigest ApprovalRecordDigest { get; }

    public CanonicalJsonObject ToCanonicalJson(bool includeDigest)
    {
        var result = new CanonicalJsonObject()
            .Add("approval_id", ApprovalId)
            .Add("target_type", TargetType)
            .Add("target_id", TargetId)
            .Add("target_digest", TargetDigest.ToString())
            .Add("policy_id", PolicyId)
            .Add("policy_version", PolicyVersion)
            .Add("policy_mode", PolicyModeWireValue(PolicyMode))
            .Add("decision", DecisionWireValue(Decision))
            .Add("approved_by", ApprovedBy.ToString())
            .AddTimestamp("approved_at", ApprovedAt);

        if (Role is not null)
        {
            result.Add("role", Role);
        }
        if (Rationale is not null)
        {
            result.Add("rationale", Rationale);
        }
        if (SupersedesApprovalId is not null)
        {
            result.Add("supersedes_approval_id", SupersedesApprovalId);
        }
        if (includeDigest)
        {
            result.Add("approval_record_digest", ApprovalRecordDigest.ToString());
        }

        return result;
    }

    public DigestEnvelope ToDigestEnvelope() => new(
        DigestScope.ApprovalRecord,
        "nexus.protocol-supplemental-approval",
        "1.0.0",
        ToCanonicalJson(includeDigest: false));

    internal static ContentDigest RequireDigest(ContentDigest digest, string name) => digest.IsValid
        ? digest
        : throw new ProtocolRuleException(ProtocolErrorCodes.StaleContentDigest, $"{name} must be a valid digest.");

    internal static ActorId RequireActor(ActorId actor) => !string.IsNullOrWhiteSpace(actor.Value)
        ? actor
        : throw new ProtocolRuleException(ProtocolErrorCodes.MissingApprovalActor, "Approval actor is required.");

    internal static DateTimeOffset RequireUtc(DateTimeOffset value, string name) => CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true)
        ? value
        : throw new ProtocolRuleException(ProtocolErrorCodes.StaleContentDigest, $"{name} must be UTC.");

    private static string PolicyModeWireValue(ApprovalPolicyMode mode) => mode switch
    {
        ApprovalPolicyMode.SingleResearcher => "single_researcher",
        ApprovalPolicyMode.DualIndependent => "dual_independent",
        ApprovalPolicyMode.Methodologist => "methodologist",
        ApprovalPolicyMode.InformationSpecialist => "information_specialist",
        ApprovalPolicyMode.Statistician => "statistician",
        ApprovalPolicyMode.ProjectOwner => "project_owner",
        ApprovalPolicyMode.InstitutionalSignoff => "institutional_signoff",
        ApprovalPolicyMode.CustomRoleExpression => "custom_role_expression",
        _ => throw new ProtocolRuleException(ProtocolErrorCodes.UnauthorizedApproval, "Unsupported approval policy mode.")
    };

    private static string DecisionWireValue(ProtocolApprovalDecision decision) => decision switch
    {
        ProtocolApprovalDecision.Approved => "approved",
        ProtocolApprovalDecision.Rejected => "rejected",
        ProtocolApprovalDecision.ChangesRequested => "changes_requested",
        ProtocolApprovalDecision.Withdrawn => "withdrawn",
        _ => throw new ProtocolRuleException(ProtocolErrorCodes.UnauthorizedApproval, "Unsupported approval decision.")
    };
}

public sealed class VerifiedProtocolSupplementalApproval
{
    internal VerifiedProtocolSupplementalApproval(ProtocolSupplementalApproval approval)
    {
        Approval = approval ?? throw new ArgumentNullException(nameof(approval));
        if (approval.ToDigestEnvelope().ComputeDigest() != approval.ApprovalRecordDigest)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.ApprovalTargetMismatch, "Supplemental approval digest is invalid.");
        }
    }

    public ProtocolSupplementalApproval Approval { get; }
}

public sealed class VerifiedProtocolWaiver
{
    internal VerifiedProtocolWaiver(
        ProtocolWaiver waiver,
        ContentDigest waiverDigest,
        ApprovalPolicy policy,
        IEnumerable<VerifiedProtocolSupplementalApproval> approvals)
    {
        Waiver = waiver;
        WaiverDigest = waiverDigest;
        Policy = policy;
        Approvals = Array.AsReadOnly(approvals.ToArray());
    }

    public ProtocolWaiver Waiver { get; }
    public ContentDigest WaiverDigest { get; }
    public ApprovalPolicy Policy { get; }
    public IReadOnlyList<VerifiedProtocolSupplementalApproval> Approvals { get; }
}

public sealed class VerifiedProtocolAmendment
{
    internal VerifiedProtocolAmendment(
        ProtocolAmendment amendment,
        ContentDigest amendmentDigest,
        ApprovalPolicy policy,
        VerifiedProtocolVersion previousVersion,
        VerifiedProtocolVersion producedVersion,
        IEnumerable<VerifiedProtocolSupplementalApproval> approvals)
    {
        Amendment = amendment;
        AmendmentDigest = amendmentDigest;
        Policy = policy;
        PreviousVersion = previousVersion;
        ProducedVersion = producedVersion;
        Approvals = Array.AsReadOnly(approvals.ToArray());
        InvalidationNotices = Array.AsReadOnly(amendment.InvalidationNotices.ToArray());
    }

    public ProtocolAmendment Amendment { get; }
    public ContentDigest AmendmentDigest { get; }
    public ApprovalPolicy Policy { get; }
    public VerifiedProtocolVersion PreviousVersion { get; }
    public VerifiedProtocolVersion ProducedVersion { get; }
    public IReadOnlyList<VerifiedProtocolSupplementalApproval> Approvals { get; }
    public IReadOnlyList<ProtocolInvalidationNotice> InvalidationNotices { get; }
}

public static partial class ProtocolSupplementalAuthorityRehydrator
{
    public static VerifiedProtocolSupplementalApproval RehydrateApproval(
        UnverifiedProtocolSupplementalApproval input,
        string expectedTargetType,
        string expectedTargetId,
        ContentDigest expectedTargetDigest,
        ApprovalPolicy policy,
        IProtocolSupplementalAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(resolver);

        if (!resolver.IsHumanActor(input.ApprovedBy))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.NonHumanApprovalActor, "Supplemental approval requires a resolved human actor.");
        }

        var approval = new ProtocolSupplementalApproval(
            input.ApprovalId,
            input.TargetType,
            input.TargetId,
            input.TargetDigest,
            input.PolicyId,
            input.PolicyVersion,
            input.PolicyMode,
            input.Decision,
            input.ApprovedBy,
            input.ApprovedAt,
            input.Role,
            input.Rationale,
            input.SupersedesApprovalId,
            input.ApprovalRecordDigest);

        if (!string.Equals(approval.TargetType, expectedTargetType, StringComparison.Ordinal) ||
            !string.Equals(approval.TargetId, expectedTargetId, StringComparison.Ordinal) ||
            approval.TargetDigest != expectedTargetDigest ||
            !PolicyMatches(approval, policy) ||
            approval.ToDigestEnvelope().ComputeDigest() != approval.ApprovalRecordDigest)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.ApprovalTargetMismatch, "Supplemental approval target, policy, or digest is invalid.");
        }

        return new VerifiedProtocolSupplementalApproval(approval);
    }

    public static VerifiedProtocolWaiver RehydrateWaiver(
        UnverifiedProtocolWaiver input,
        IProtocolSupplementalAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resolver);
        var waiver = CloneWaiver(input.Waiver ?? throw new ArgumentNullException(nameof(input.Waiver)));
        ValidateWaiver(waiver, resolver);
        var digest = ContentDigest.Sha256CanonicalJson(waiver.ToCanonicalJson());
        var policy = resolver.ResolvePolicy(ProtocolSupplementalTargetTypes.Waiver, waiver.WaiverId)
            ?? throw new ProtocolRuleException(ProtocolErrorCodes.InvalidWaiver, "Waiver approval policy could not be resolved.");
        if (!string.Equals(waiver.ApprovalPolicyId, policy.PolicyId, StringComparison.Ordinal))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidWaiver, "Waiver approval policy does not match resolved authority.");
        }
        var approvals = ResolveAndValidateApprovals(
            waiver.ApprovalIds,
            ProtocolSupplementalTargetTypes.Waiver,
            waiver.WaiverId,
            digest,
            policy,
            resolver);
        return new VerifiedProtocolWaiver(waiver, digest, policy, approvals);
    }

    public static VerifiedProtocolAmendment RehydrateAmendment(
        UnverifiedProtocolAmendment input,
        IProtocolSupplementalAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resolver);
        var amendment = CloneAmendment(input.Amendment ?? throw new ArgumentNullException(nameof(input.Amendment)));
        ValidateAmendmentNotices(amendment, resolver);
        var previous = resolver.ResolveProtocolVersion(amendment.AmendsVersionId)
            ?? throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Previous Protocol version could not be resolved.");
        var produced = resolver.ResolveProtocolVersion(amendment.ProducesVersionId)
            ?? throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Produced Protocol version could not be resolved.");
        ValidateLineage(amendment, previous.Version, produced.Version);
        var digest = ContentDigest.Sha256CanonicalJson(amendment.ToCanonicalJson());
        var policy = resolver.ResolvePolicy(ProtocolSupplementalTargetTypes.Amendment, amendment.AmendmentId)
            ?? throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Amendment approval policy could not be resolved.");
        if (!string.Equals(amendment.ApprovalPolicyId, policy.PolicyId, StringComparison.Ordinal))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Amendment approval policy does not match resolved authority.");
        }
        var approvals = ResolveAndValidateApprovals(
            amendment.ApprovalIds,
            ProtocolSupplementalTargetTypes.Amendment,
            amendment.AmendmentId,
            digest,
            policy,
            resolver);
        return new VerifiedProtocolAmendment(amendment, digest, policy, previous, produced, approvals);
    }

    internal static VerifiedProtocolSupplementalApproval[] ResolveAndValidateApprovals(
        IReadOnlyList<string> approvalIds,
        string targetType,
        string targetId,
        ContentDigest targetDigest,
        ApprovalPolicy policy,
        IProtocolSupplementalAuthorityResolver resolver)
    {
        if (policy.AllowsAutomation)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.UnauthorizedApproval, "Supplemental approval policy cannot allow automation.");
        }

        var ids = approvalIds.Select(id => Guard.NotBlank(id, nameof(approvalIds))).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Length != policy.MinimumApprovals)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InsufficientApprovalPolicy, "Supplemental authority requires the exact approval set selected by policy.");
        }

        var verified = ids.Select(id => resolver.ResolveApproval(id)
            ?? throw new ProtocolRuleException(ProtocolErrorCodes.InsufficientApprovalPolicy, $"Approval '{id}' could not be resolved.")).ToArray();
        var approvals = verified.Select(item => item.Approval).ToArray();
        if (approvals.Any(item => item.Decision != ProtocolApprovalDecision.Approved ||
            item.TargetType != targetType || item.TargetId != targetId || item.TargetDigest != targetDigest ||
            !PolicyMatches(item, policy) || !resolver.IsHumanActor(item.ApprovedBy)))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.ApprovalTargetMismatch, "Supplemental approval does not match target authority.");
        }

        if (policy.RequiresDistinctActors && approvals.Select(item => item.ApprovedBy).Distinct().Count() != approvals.Length)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.SameActorDualApproval, "Supplemental approvals require distinct actors.");
        }

        foreach (var role in policy.RequiredRoles)
        {
            if (!approvals.Any(item => string.Equals(item.Role, role, StringComparison.Ordinal)))
            {
                throw new ProtocolRuleException(ProtocolErrorCodes.InsufficientApprovalPolicy, $"Required supplemental approval role '{role}' is missing.");
            }
        }

        return verified;
    }

    private static void ValidateWaiver(ProtocolWaiver waiver, IProtocolSupplementalAuthorityResolver resolver)
    {
        _ = waiver.ToCanonicalJson();
        if (!resolver.IsHumanActor(waiver.RequestedBy))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidWaiver, "Waiver requester must resolve to a human actor.");
        }
        if (waiver.ApprovalIds.Count == 0)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidWaiver, "Waiver approval references are required.");
        }
    }

    private static void ValidateAmendmentNotices(ProtocolAmendment amendment, IProtocolSupplementalAuthorityResolver resolver)
    {
        _ = amendment.ToCanonicalJson();
        if (!resolver.IsHumanActor(amendment.RequestedBy))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Amendment requester must resolve to a human actor.");
        }
        var noticeIds = amendment.InvalidationNotices.Select(item => Guard.NotBlank(item.NoticeId, nameof(item.NoticeId))).ToArray();
        if (noticeIds.Length == 0 || noticeIds.Distinct(StringComparer.Ordinal).Count() != noticeIds.Length)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Amendment invalidation notice membership must be present and unique.");
        }
        foreach (var notice in amendment.InvalidationNotices)
        {
            if (notice.SourceAmendmentId != amendment.AmendmentId ||
                !amendment.ChangedDecisionKeys.Contains(notice.AffectedRequirementId, StringComparer.Ordinal) ||
                !notice.AffectedArtifactDigest.IsValid)
            {
                throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Invalidation notice is foreign, malformed, or outside amendment membership.");
            }
        }
        if (amendment.ChangedDecisionKeys.Any(key =>
            amendment.InvalidationNotices.Count(notice =>
                string.Equals(notice.AffectedRequirementId, key, StringComparison.Ordinal)) != 1))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Every changed decision requires exactly one invalidation notice.");
        }
    }

    private static void ValidateLineage(ProtocolAmendment amendment, ProtocolVersion previous, ProtocolVersion produced)
    {
        if (previous.Status != ProtocolStatus.Approved || produced.Status != ProtocolStatus.Approved ||
            amendment.ProtocolId != previous.ProtocolId || amendment.ProtocolId != produced.ProtocolId ||
            amendment.AmendsVersionId != previous.Id || amendment.ProducesVersionId != produced.Id ||
            amendment.PreviousContentDigest != previous.ContentDigest ||
            produced.AmendmentId != amendment.AmendmentId || produced.SupersedesVersionId != previous.Id)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidAmendment, "Amendment Protocol version lineage is invalid.");
        }
    }

    private static bool PolicyMatches(ProtocolSupplementalApproval approval, ApprovalPolicy policy) =>
        approval.PolicyId == policy.PolicyId && approval.PolicyVersion == policy.PolicyVersion && approval.PolicyMode == policy.Mode;

    private static ProtocolWaiver CloneWaiver(ProtocolWaiver source) => new(
        source.WaiverId,
        source.AffectedRequirementId,
        source.Condition,
        source.ExpiresAt,
        source.Rationale,
        source.ConsequenceWarning,
        source.DisclosureMapping,
        source.RequestedBy,
        source.RequestedAt,
        source.ApprovalPolicyId,
        Array.AsReadOnly(source.ApprovalIds.ToArray()));

    private static ProtocolAmendment CloneAmendment(ProtocolAmendment source) => new(
        source.AmendmentId,
        source.ProtocolId,
        source.AmendsVersionId,
        source.ProducesVersionId,
        source.PreviousContentDigest,
        source.RequestedBy,
        source.RequestedAt,
        source.Rationale,
        Array.AsReadOnly(source.ChangedDecisionKeys.ToArray()),
        Array.AsReadOnly(source.InvalidationNotices.ToArray()),
        source.InvalidationPlanDigest,
        source.ApprovalPolicyId,
        Array.AsReadOnly(source.ApprovalIds.ToArray()));
}
