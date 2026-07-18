using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public sealed record UnverifiedProtocolApproval(
    string ApprovalId,
    string TargetType,
    string TargetId,
    string ProtocolId,
    string ProtocolVersionId,
    int ProtocolVersionNumber,
    ContentDigest ContentDigest,
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

public sealed record UnverifiedProtocolVersion(
    string Id,
    string ProtocolId,
    string ProjectId,
    int VersionNumber,
    ProtocolStatus Status,
    ProtocolTemplate Template,
    ProtocolIntent Intent,
    CanonicalJsonObject Values,
    IReadOnlyList<RequiredDecisionDefinition> RequiredDecisions,
    IReadOnlyList<ProtocolDecision> Decisions,
    IReadOnlyList<ProtocolWaiver> Waivers,
    IReadOnlyList<UnresolvedDecision> UnresolvedDecisions,
    ContentDigest ContentDigest,
    ApprovalPolicy ApprovalPolicy,
    IReadOnlyList<string> ApprovalIds,
    DateTimeOffset ApprovedAt,
    string? SupersedesVersionId = null,
    string? SupersededByVersionId = null,
    string? AmendmentId = null);

public interface IProtocolAuthorityResolver
{
    ApprovalPolicy ResolveApprovalPolicy(ProtocolTemplate template);

    bool IsHumanActor(ActorId actorId);

    VerifiedProtocolApproval ResolveApproval(string approvalId);
}

public sealed class VerifiedProtocolApproval
{
    internal VerifiedProtocolApproval(ProtocolApproval approval)
    {
        Approval = approval ?? throw new ArgumentNullException(nameof(approval));
        if (!approval.HasValidApprovalRecordDigest() || !approval.IsApprovedByHuman())
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "Verified approval state requires a valid approval-record digest and a resolved human actor.");
        }
    }

    public ProtocolApproval Approval { get; }
}

public sealed class VerifiedProtocolVersion
{
    internal VerifiedProtocolVersion(
        ProtocolVersion version,
        ApprovalPolicy approvalPolicy,
        IReadOnlyList<VerifiedProtocolApproval> approvals)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        ApprovalPolicy = approvalPolicy ?? throw new ArgumentNullException(nameof(approvalPolicy));
        Approvals = Array.AsReadOnly((approvals ?? throw new ArgumentNullException(nameof(approvals))).ToArray());

        if (version.Status is not ProtocolStatus.Approved and not ProtocolStatus.Superseded ||
            version.ToProtocolContentDigestEnvelope().ComputeDigest() != version.ContentDigest)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Verified protocol state requires approved authority and a reproducible content digest.");
        }
    }

    public ProtocolVersion Version { get; }

    public ApprovalPolicy ApprovalPolicy { get; }

    public IReadOnlyList<VerifiedProtocolApproval> Approvals { get; }
}

public static class ProtocolRehydrator
{
    private const string ProtocolVersionTargetType = "protocol-version";

    public static VerifiedProtocolApproval RehydrateApproval(
        UnverifiedProtocolApproval input,
        ProtocolVersion candidate,
        ApprovalPolicy policy,
        IProtocolAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(resolver);

        if (candidate.Status != ProtocolStatus.ReadyForReview)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Protocol approvals can only be rehydrated against a review candidate.");
        }

        if (!resolver.IsHumanActor(input.ApprovedBy))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.NonHumanApprovalActor,
                "A resolved human actor is required for protocol approval authority.");
        }

        if (input.PolicyMode != policy.Mode)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "The persisted approval policy mode does not match the resolved policy.");
        }

        var approval = new ProtocolApproval(
            Guard.NotBlank(input.ApprovalId, nameof(input.ApprovalId)),
            Guard.NotBlank(input.TargetType, nameof(input.TargetType)),
            Guard.NotBlank(input.TargetId, nameof(input.TargetId)),
            Guard.NotBlank(input.ProtocolId, nameof(input.ProtocolId)),
            Guard.NotBlank(input.ProtocolVersionId, nameof(input.ProtocolVersionId)),
            input.ProtocolVersionNumber,
            RequireDigest(input.ContentDigest, nameof(input.ContentDigest)),
            Guard.NotBlank(input.PolicyId, nameof(input.PolicyId)),
            Guard.NotBlank(input.PolicyVersion, nameof(input.PolicyVersion)),
            PolicyModeWireValue(input.PolicyMode),
            input.Decision,
            RequireActor(input.ApprovedBy),
            RequireUtc(input.ApprovedAt, nameof(input.ApprovedAt)),
            input.Role,
            input.Rationale,
            input.SupersedesApprovalId,
            true,
            RequireDigest(input.ApprovalRecordDigest, nameof(input.ApprovalRecordDigest)));

        if (!approval.HasValidApprovalRecordDigest())
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "The approval-record digest does not reproduce from persisted approval content.");
        }

        if (!approval.IsBoundToTarget(candidate, policy) ||
            !string.Equals(approval.TargetType, ProtocolVersionTargetType, StringComparison.Ordinal))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "The persisted approval does not bind the resolved protocol candidate and policy.");
        }

        return new VerifiedProtocolApproval(approval);
    }

    public static VerifiedProtocolVersion RehydrateVersion(
        UnverifiedProtocolVersion input,
        IProtocolAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resolver);

        if (input.Status is not ProtocolStatus.Approved and not ProtocolStatus.Superseded)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Authority-safe protocol rehydration accepts approved or superseded versions only.");
        }

        ValidateVersionContent(input, resolver);
        var policy = resolver.ResolveApprovalPolicy(input.Template)
            ?? throw new ProtocolRuleException(
                ProtocolErrorCodes.InsufficientApprovalPolicy,
                "The pinned protocol template has no resolved approval policy.");

        var claimedPolicy = input.ApprovalPolicy
            ?? throw new ProtocolRuleException(
                ProtocolErrorCodes.InsufficientApprovalPolicy,
                "The persisted protocol version must carry its claimed approval policy.");
        if (policy.AllowsAutomation || !PoliciesMatch(policy, claimedPolicy))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.UnauthorizedApproval,
                "The persisted approval policy does not match the policy resolved for the pinned template.");
        }

        var candidate = CreateCandidate(input, policy);
        var expectedDigest = candidate.ToProtocolContentDigestEnvelope().ComputeDigest();
        if (expectedDigest != input.ContentDigest)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "The protocol-content digest does not reproduce from persisted protocol content.");
        }

        var approvalIds = input.ApprovalIds
            ?? throw new ProtocolRuleException(
                ProtocolErrorCodes.InsufficientApprovalPolicy,
                "Approved protocol versions require approval identifiers.");
        EnsureUnique(approvalIds, "approval identifiers", ProtocolErrorCodes.InsufficientApprovalPolicy);

        var verifiedApprovals = approvalIds
            .Select(id => resolver.ResolveApproval(Guard.NotBlank(id, nameof(input.ApprovalIds)))
                ?? throw new ProtocolRuleException(
                    ProtocolErrorCodes.InsufficientApprovalPolicy,
                    $"Approval '{id}' could not be resolved."))
            .ToArray();
        var approvals = verifiedApprovals.Select(item => item.Approval).ToArray();
        var matchingApprovals = ProtocolDraft.ValidateApprovalPolicy(candidate, policy, approvals);

        if (matchingApprovals.Length != policy.MinimumApprovals)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.InsufficientApprovalPolicy,
                "Approved version authority must contain exactly the approvals required by its resolved policy.");
        }

        var matchingIds = matchingApprovals.Select(approval => approval.ApprovalId).ToHashSet(StringComparer.Ordinal);
        if (matchingIds.Count != approvalIds.Count || approvalIds.Any(id => !matchingIds.Contains(id)))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.InsufficientApprovalPolicy,
                "Approved version authority must contain exactly the resolved approvals that satisfy its policy.");
        }

        var derivedApprovedAt = matchingApprovals.Max(approval => approval.ApprovedAt);
        if (RequireUtc(input.ApprovedAt, nameof(input.ApprovedAt)) != derivedApprovedAt)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "The protocol approval timestamp must equal the latest approval that satisfied the resolved policy.");
        }

        var version = new ProtocolVersion(
            candidate.Id,
            candidate.ProtocolId,
            candidate.ProjectId,
            candidate.VersionNumber,
            input.Status,
            candidate.Template,
            candidate.Intent,
            candidate.Values,
            candidate.RequiredDecisions,
            candidate.Decisions,
            candidate.Waivers,
            candidate.ContentDigest,
            candidate.ApprovalPolicyId,
            approvalIds,
            derivedApprovedAt,
            candidate.SupersedesVersionId,
            input.SupersededByVersionId,
            candidate.AmendmentId,
            candidate.UnresolvedDecisions);

        return new VerifiedProtocolVersion(version, policy, verifiedApprovals);
    }

    private static ProtocolVersion CreateCandidate(UnverifiedProtocolVersion input, ApprovalPolicy policy)
    {
        return new ProtocolVersion(
            Guard.NotBlank(input.Id, nameof(input.Id)),
            Guard.NotBlank(input.ProtocolId, nameof(input.ProtocolId)),
            Guard.NotBlank(input.ProjectId, nameof(input.ProjectId)),
            input.VersionNumber,
            ProtocolStatus.ReadyForReview,
            input.Template ?? throw new ArgumentNullException(nameof(input.Template)),
            input.Intent ?? throw new ArgumentNullException(nameof(input.Intent)),
            input.Values ?? throw new ArgumentNullException(nameof(input.Values)),
            input.RequiredDecisions ?? throw new ArgumentNullException(nameof(input.RequiredDecisions)),
            input.Decisions ?? throw new ArgumentNullException(nameof(input.Decisions)),
            input.Waivers ?? throw new ArgumentNullException(nameof(input.Waivers)),
            RequireDigest(input.ContentDigest, nameof(input.ContentDigest)),
            policy.PolicyId,
            Array.Empty<string>(),
            null,
            input.SupersedesVersionId,
            input.SupersededByVersionId,
            input.AmendmentId,
            input.UnresolvedDecisions ?? throw new ArgumentNullException(nameof(input.UnresolvedDecisions)));
    }

    private static void ValidateVersionContent(UnverifiedProtocolVersion input, IProtocolAuthorityResolver resolver)
    {
        if (input.VersionNumber <= 0)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.StaleContentDigest, "Protocol version numbers must be positive.");
        }

        RequireDigest(input.ContentDigest, nameof(input.ContentDigest));
        _ = input.Template?.ToCanonicalJson() ?? throw new ArgumentNullException(nameof(input.Template));
        _ = input.Intent?.ToCanonicalJson() ?? throw new ArgumentNullException(nameof(input.Intent));
        _ = input.Values ?? throw new ArgumentNullException(nameof(input.Values));

        var required = input.RequiredDecisions ?? throw new ArgumentNullException(nameof(input.RequiredDecisions));
        var decisions = input.Decisions ?? throw new ArgumentNullException(nameof(input.Decisions));
        var waivers = input.Waivers ?? throw new ArgumentNullException(nameof(input.Waivers));
        var unresolved = input.UnresolvedDecisions ?? throw new ArgumentNullException(nameof(input.UnresolvedDecisions));

        EnsureUnique(required.Select(item => item.DecisionKey), "required decision keys", ProtocolErrorCodes.DuplicateDecision);
        EnsureUnique(decisions.Select(item => item.DecisionId), "decision identifiers", ProtocolErrorCodes.DuplicateDecision);
        EnsureUnique(decisions.Select(item => item.DecisionKey), "decision keys", ProtocolErrorCodes.DuplicateDecision);
        EnsureUnique(waivers.Select(item => item.WaiverId), "waiver identifiers", ProtocolErrorCodes.InvalidWaiver);
        EnsureUnique(unresolved.Select(item => item.UnresolvedId), "unresolved decision identifiers", ProtocolErrorCodes.DuplicateDecision);
        EnsureUnique(unresolved.Select(item => item.DecisionKey), "unresolved decision keys", ProtocolErrorCodes.DuplicateDecision);

        if (unresolved.Any(item => item.BlocksProtocolApproval))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.BlockingUnresolvedDecision,
                "Approved protocol versions cannot contain blocking unresolved decisions.");
        }

        foreach (var actor in decisions.Select(item => item.DecidedBy)
                     .Concat(unresolved.Select(item => item.CreatedBy))
                     .Concat(waivers.Select(item => item.RequestedBy)))
        {
            if (string.IsNullOrWhiteSpace(actor.Value) || !resolver.IsHumanActor(actor))
            {
                throw new ProtocolRuleException(
                    ProtocolErrorCodes.NonHumanApprovalActor,
                    "Approved protocol content requires resolved human actors for decisions, unresolved records, and waivers.");
            }
        }

        if (input.Status == ProtocolStatus.Superseded && string.IsNullOrWhiteSpace(input.SupersededByVersionId))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Superseded protocol versions require a successor version identifier.");
        }

        foreach (var definition in required)
        {
            var covered = decisions.Any(item => string.Equals(item.DecisionKey, definition.DecisionKey, StringComparison.Ordinal)) ||
                unresolved.Any(item => string.Equals(item.DecisionKey, definition.DecisionKey, StringComparison.Ordinal)) ||
                waivers.Any(item => string.Equals(item.AffectedRequirementId, definition.SourceRequirementId, StringComparison.Ordinal));
            if (!covered)
            {
                throw new ProtocolRuleException(
                    ProtocolErrorCodes.MissingRequiredDecision,
                    $"Required protocol decision '{definition.DecisionKey}' is not resolved.");
            }
        }
    }

    private static void EnsureUnique(IEnumerable<string> values, string description, string category)
    {
        var normalized = values.Select(value => Guard.NotBlank(value, description)).ToArray();
        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new ProtocolRuleException(category, $"Protocol {description} must be unique.");
        }
    }

    private static ContentDigest RequireDigest(ContentDigest digest, string name)
    {
        if (!digest.IsValid)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.StaleContentDigest, $"{name} must be a valid digest.");
        }

        return digest;
    }

    private static ActorId RequireActor(ActorId actor)
    {
        if (string.IsNullOrWhiteSpace(actor.Value))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.MissingApprovalActor, "Approval actor is required.");
        }

        return actor;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name)
    {
        if (!CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true))
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.StaleContentDigest, $"{name} must be a canonical UTC timestamp.");
        }

        return value;
    }

    private static string PolicyModeWireValue(ApprovalPolicyMode mode)
    {
        return mode switch
        {
            ApprovalPolicyMode.SingleResearcher => "single_researcher",
            ApprovalPolicyMode.DualIndependent => "dual_independent",
            ApprovalPolicyMode.Methodologist => "methodologist",
            ApprovalPolicyMode.InformationSpecialist => "information_specialist",
            ApprovalPolicyMode.Statistician => "statistician",
            ApprovalPolicyMode.ProjectOwner => "project_owner",
            ApprovalPolicyMode.InstitutionalSignoff => "institutional_signoff",
            ApprovalPolicyMode.CustomRoleExpression => "custom_role_expression",
            _ => throw new ProtocolRuleException(
                ProtocolErrorCodes.UnauthorizedApproval,
                $"Unsupported approval policy mode '{mode}'.")
        };
    }

    private static bool PoliciesMatch(ApprovalPolicy resolved, ApprovalPolicy claimed)
    {
        return string.Equals(
            CanonicalJsonSerializer.Serialize(resolved.ToCanonicalJson()),
            CanonicalJsonSerializer.Serialize(claimed.ToCanonicalJson()),
            StringComparison.Ordinal);
    }
}
