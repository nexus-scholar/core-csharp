using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public sealed class ProtocolTag
{
}

public sealed class ProtocolVersionTag
{
}

public enum ProtocolStatus
{
    Draft,
    ReadyForReview,
    Approved,
    Superseded,
    Withdrawn
}

public enum ApprovalPolicyMode
{
    SingleResearcher,
    DualIndependent,
    Methodologist,
    InformationSpecialist,
    Statistician,
    ProjectOwner,
    InstitutionalSignoff,
    CustomRoleExpression
}

public enum ProtocolApprovalDecision
{
    Approved,
    Rejected,
    ChangesRequested,
    Withdrawn
}

public static class ProtocolErrorCodes
{
    public const string MissingRequiredDecision = "missing-required-decision";
    public const string BlockingUnresolvedDecision = "blocking-unresolved-decision";
    public const string DuplicateDecision = "duplicate-decision";
    public const string PostApprovalMutation = "post-approval-mutation";
    public const string UnauthorizedApproval = "unauthorized-approval";
    public const string StaleContentDigest = "stale-content-digest";
    public const string InvalidAmendment = "invalid-amendment";
    public const string InvalidWaiver = "invalid-waiver";
    public const string InvalidDeviation = "invalid-deviation";
    public const string SameActorDualApproval = "same-actor-dual-approval";
    public const string AutomationCannotApprove = "automation-cannot-approve";
}

public sealed class ProtocolRuleException : DomainRuleException
{
    public ProtocolRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public sealed record ProtocolActor(ActorId Id, bool IsHuman)
{
    public static ProtocolActor Human(string actorId) => new(ActorId.From(actorId), true);

    public static ProtocolActor Human(ActorId actorId) => new(actorId, true);

    public static ProtocolActor Automation(string actorId) => new(ActorId.From(actorId), false);
}

public sealed record ProtocolTemplate(
    string TemplateId,
    string TemplateVersion,
    ContentDigest TemplateDigest)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("template_digest", TemplateDigest.ToString())
            .Add("template_id", Guard.NotBlank(TemplateId, nameof(TemplateId)))
            .Add("template_version", Guard.NotBlank(TemplateVersion, nameof(TemplateVersion)));
    }
}

public sealed record ProtocolIntent(
    string RawSubject,
    string ReviewGoal,
    string? SelectedReviewFamily = null)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .Add("raw_subject", Guard.NotBlank(RawSubject, nameof(RawSubject)))
            .Add("review_goal", Guard.NotBlank(ReviewGoal, nameof(ReviewGoal)));

        if (SelectedReviewFamily is not null)
        {
            result.Add("selected_review_family", Guard.NotBlank(SelectedReviewFamily, nameof(SelectedReviewFamily)));
        }

        return result;
    }
}

public sealed record RequiredDecisionDefinition(
    string DecisionKey,
    string Title,
    string Description,
    CanonicalJsonValue ValueSchema,
    string RequiredBefore,
    string ApprovalGateId,
    string SourceRequirementId,
    bool AllowsUnresolved)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("allows_unresolved", AllowsUnresolved)
            .Add("approval_gate_id", Guard.NotBlank(ApprovalGateId, nameof(ApprovalGateId)))
            .Add("decision_key", Guard.NotBlank(DecisionKey, nameof(DecisionKey)))
            .Add("description", Description ?? string.Empty)
            .Add("required_before", Guard.NotBlank(RequiredBefore, nameof(RequiredBefore)))
            .Add("source_requirement_id", Guard.NotBlank(SourceRequirementId, nameof(SourceRequirementId)))
            .Add("title", Guard.NotBlank(Title, nameof(Title)))
            .Add("value_schema", CanonicalJsonValue.DeepClone(ValueSchema ?? throw new ArgumentNullException(nameof(ValueSchema))));
    }
}

public sealed record ProtocolDecision(
    string DecisionId,
    string DecisionKey,
    CanonicalJsonValue Value,
    string? Rationale,
    ActorId DecidedBy,
    DateTimeOffset DecidedAt,
    ContentDigest? SourceProposalDigest = null,
    string? SupersedesDecisionId = null)
{
    public string Key => DecisionKey;

    public CanonicalJsonObject ToCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .AddTimestamp("decided_at", DecidedAt)
            .Add("decided_by", DecidedBy.ToString())
            .Add("decision_id", Guard.NotBlank(DecisionId, nameof(DecisionId)))
            .Add("decision_key", Guard.NotBlank(DecisionKey, nameof(DecisionKey)))
            .Add("value", CanonicalJsonValue.DeepClone(Value ?? throw new ArgumentNullException(nameof(Value))));

        if (Rationale is not null)
        {
            result.Add("rationale", Rationale);
        }

        if (SourceProposalDigest is not null)
        {
            result.Add("source_proposal_digest", SourceProposalDigest.Value.ToString());
        }

        if (SupersedesDecisionId is not null)
        {
            result.Add("supersedes_decision_id", SupersedesDecisionId);
        }

        return result;
    }
}

public sealed record UnresolvedDecision(
    string UnresolvedId,
    string DecisionKey,
    string Question,
    string Reason,
    string RequiredBefore,
    ActorId CreatedBy,
    DateTimeOffset CreatedAt,
    bool BlocksProtocolApproval)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("blocks_protocol_approval", BlocksProtocolApproval)
            .AddTimestamp("created_at", CreatedAt)
            .Add("created_by", CreatedBy.ToString())
            .Add("decision_key", Guard.NotBlank(DecisionKey, nameof(DecisionKey)))
            .Add("question", Guard.NotBlank(Question, nameof(Question)))
            .Add("reason", Guard.NotBlank(Reason, nameof(Reason)))
            .Add("required_before", Guard.NotBlank(RequiredBefore, nameof(RequiredBefore)))
            .Add("unresolved_id", Guard.NotBlank(UnresolvedId, nameof(UnresolvedId)));
    }
}

public sealed record ApprovalPolicy(
    string PolicyId,
    string PolicyVersion,
    ApprovalPolicyMode Mode,
    IReadOnlyList<string> RequiredRoles,
    int MinimumApprovals,
    bool RequiresDistinctActors,
    bool AllowsAutomation,
    string? MethodPackId = null,
    string? CustomRuleId = null)
{
    public static ApprovalPolicy ExplicitCustomSingleResearcher(string policyId = "custom-local-single-researcher")
    {
        return new ApprovalPolicy(
            policyId,
            "1.0.0",
            ApprovalPolicyMode.SingleResearcher,
            Array.Empty<string>(),
            1,
            false,
            false,
            CustomRuleId: "explicit-custom-local-review");
    }

    public static ApprovalPolicy DualIndependent(string policyId = "dual-independent")
    {
        return new ApprovalPolicy(
            policyId,
            "1.0.0",
            ApprovalPolicyMode.DualIndependent,
            Array.Empty<string>(),
            2,
            true,
            false);
    }

    public IReadOnlyList<string> RequiredRoles { get; } = (RequiredRoles ?? Array.Empty<string>()).ToArray();

    public string ModeWireValue => Mode switch
    {
        ApprovalPolicyMode.SingleResearcher => "single_researcher",
        ApprovalPolicyMode.DualIndependent => "dual_independent",
        ApprovalPolicyMode.Methodologist => "methodologist",
        ApprovalPolicyMode.InformationSpecialist => "information_specialist",
        ApprovalPolicyMode.Statistician => "statistician",
        ApprovalPolicyMode.ProjectOwner => "project_owner",
        ApprovalPolicyMode.InstitutionalSignoff => "institutional_signoff",
        ApprovalPolicyMode.CustomRoleExpression => "custom_role_expression",
        _ => throw new InvalidOperationException($"Unsupported approval policy mode '{Mode}'.")
    };

    public CanonicalJsonObject ToCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .Add("allows_automation", AllowsAutomation)
            .Add("minimum_approvals", MinimumApprovals)
            .Add("mode", ModeWireValue)
            .Add("policy_id", Guard.NotBlank(PolicyId, nameof(PolicyId)))
            .Add("policy_version", Guard.NotBlank(PolicyVersion, nameof(PolicyVersion)))
            .Add("requires_distinct_actors", RequiresDistinctActors)
            .Add("required_roles", CanonicalJsonValue.Array(
                RequiredRoles.OrderBy(role => role, StringComparer.Ordinal)
                    .Select(CanonicalJsonValue.From)
                    .ToArray()));

        if (MethodPackId is not null)
        {
            result.Add("method_pack_id", MethodPackId);
        }

        if (CustomRuleId is not null)
        {
            result.Add("custom_rule_id", CustomRuleId);
        }

        return result;
    }
}

public sealed record ProtocolApproval(
    string ApprovalId,
    string TargetType,
    string TargetId,
    string ProtocolId,
    string ProtocolVersionId,
    int ProtocolVersionNumber,
    ContentDigest ContentDigest,
    string PolicyId,
    string PolicyVersion,
    string PolicyMode,
    ProtocolApprovalDecision Decision,
    ActorId ApprovedBy,
    DateTimeOffset ApprovedAt,
    string? Role,
    string? Rationale,
    string? SupersedesApprovalId,
    ContentDigest ApprovalRecordDigest)
{
    public static ProtocolApproval Create(
        IIdGenerator ids,
        ProtocolVersion version,
        ApprovalPolicy policy,
        ProtocolActor actor,
        IClock clock,
        ContentDigest expectedContentDigest,
        ProtocolApprovalDecision decision = ProtocolApprovalDecision.Approved,
        string? role = null,
        string? rationale = null,
        string? supersedesApprovalId = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        EnsureHumanApprovalActor(actor, policy);

        if (expectedContentDigest != version.ContentDigest)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Approval must bind the current protocol-content digest.");
        }

        var approval = new ProtocolApproval(
            NewId(ids),
            "protocol-version",
            version.Id,
            version.ProtocolId,
            version.Id,
            version.VersionNumber,
            version.ContentDigest,
            policy.PolicyId,
            policy.PolicyVersion,
            policy.ModeWireValue,
            decision,
            actor.Id,
            clock.UtcNow,
            role,
            rationale,
            supersedesApprovalId,
            default);

        return approval with { ApprovalRecordDigest = approval.ComputeApprovalRecordDigest() };
    }

    public CanonicalJsonObject ToCanonicalJson(bool includeDigest)
    {
        var result = new CanonicalJsonObject()
            .Add("approval_id", Guard.NotBlank(ApprovalId, nameof(ApprovalId)))
            .AddTimestamp("approved_at", ApprovedAt)
            .Add("approved_by", ApprovedBy.ToString())
            .Add("content_digest", ContentDigest.ToString())
            .Add("decision", DecisionWireValue)
            .Add("policy_id", Guard.NotBlank(PolicyId, nameof(PolicyId)))
            .Add("policy_mode", Guard.NotBlank(PolicyMode, nameof(PolicyMode)))
            .Add("policy_version", Guard.NotBlank(PolicyVersion, nameof(PolicyVersion)))
            .Add("protocol_id", Guard.NotBlank(ProtocolId, nameof(ProtocolId)))
            .Add("protocol_version_id", Guard.NotBlank(ProtocolVersionId, nameof(ProtocolVersionId)))
            .Add("protocol_version_number", ProtocolVersionNumber)
            .Add("target_id", Guard.NotBlank(TargetId, nameof(TargetId)))
            .Add("target_type", Guard.NotBlank(TargetType, nameof(TargetType)));

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

    public DigestEnvelope ToApprovalRecordDigestEnvelope()
    {
        return new DigestEnvelope(
            DigestScope.ApprovalRecord,
            "nexus.protocol-approval-record",
            "1.0.0",
            ToCanonicalJson(includeDigest: false));
    }

    private string DecisionWireValue => Decision switch
    {
        ProtocolApprovalDecision.Approved => "approved",
        ProtocolApprovalDecision.Rejected => "rejected",
        ProtocolApprovalDecision.ChangesRequested => "changes_requested",
        ProtocolApprovalDecision.Withdrawn => "withdrawn",
        _ => throw new InvalidOperationException($"Unsupported approval decision '{Decision}'.")
    };

    private ContentDigest ComputeApprovalRecordDigest() => ToApprovalRecordDigestEnvelope().ComputeDigest();

    private static void EnsureHumanApprovalActor(ProtocolActor actor, ApprovalPolicy policy)
    {
        if (actor.Id.Value.Length == 0)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.UnauthorizedApproval, "Approval actor is required.");
        }

        if (!actor.IsHuman)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.AutomationCannotApprove,
                "Automation cannot approve protocol content.");
        }

        if (policy.AllowsAutomation)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.UnauthorizedApproval,
                "Protocol approval policies must not allow automation authority.");
        }
    }

    private static string NewId(IIdGenerator ids) => ids.NewId().ToString("D");
}

public sealed record ProtocolVersion
{
    public ProtocolVersion(
        string id,
        string protocolId,
        string projectId,
        int versionNumber,
        ProtocolStatus status,
        ProtocolTemplate template,
        ProtocolIntent intent,
        CanonicalJsonObject values,
        IEnumerable<RequiredDecisionDefinition> requiredDecisions,
        IEnumerable<ProtocolDecision> decisions,
        IEnumerable<ProtocolWaiver> waivers,
        ContentDigest contentDigest,
        string approvalPolicyId,
        IEnumerable<string> approvalIds,
        DateTimeOffset? approvedAt,
        string? supersedesVersionId = null,
        string? supersededByVersionId = null,
        string? amendmentId = null)
    {
        Id = Guard.NotBlank(id, nameof(id));
        ProtocolId = Guard.NotBlank(protocolId, nameof(protocolId));
        ProjectId = Guard.NotBlank(projectId, nameof(projectId));
        VersionNumber = versionNumber;
        Status = status;
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        Values = ((CanonicalJsonObject)CanonicalJsonValue.DeepClone(values ?? throw new ArgumentNullException(nameof(values)))).Freeze();
        RequiredDecisions = (requiredDecisions ?? throw new ArgumentNullException(nameof(requiredDecisions))).ToArray();
        Decisions = (decisions ?? throw new ArgumentNullException(nameof(decisions))).ToArray();
        Waivers = (waivers ?? throw new ArgumentNullException(nameof(waivers))).ToArray();
        ContentDigest = contentDigest;
        ApprovalPolicyId = Guard.NotBlank(approvalPolicyId, nameof(approvalPolicyId));
        ApprovalIds = (approvalIds ?? Array.Empty<string>()).Select(id => Guard.NotBlank(id, nameof(approvalIds))).ToArray();
        ApprovedAt = approvedAt;
        SupersedesVersionId = supersedesVersionId;
        SupersededByVersionId = supersededByVersionId;
        AmendmentId = amendmentId;
    }

    public string Id { get; }

    public string ProtocolId { get; }

    public string ProjectId { get; }

    public int VersionNumber { get; }

    public ProtocolStatus Status { get; }

    public ProtocolTemplate Template { get; }

    public ProtocolIntent Intent { get; }

    public CanonicalJsonObject Values { get; }

    public IReadOnlyList<RequiredDecisionDefinition> RequiredDecisions { get; }

    public IReadOnlyList<ProtocolDecision> Decisions { get; }

    public IReadOnlyList<ProtocolWaiver> Waivers { get; }

    public ContentDigest ContentDigest { get; }

    public ContentDigest Digest => ContentDigest;

    public string ApprovalPolicyId { get; }

    public IReadOnlyList<string> ApprovalIds { get; }

    public DateTimeOffset? ApprovedAt { get; }

    public string? SupersedesVersionId { get; }

    public string? SupersededByVersionId { get; }

    public string? AmendmentId { get; }

    public ProtocolVersion WithApprovals(IEnumerable<ProtocolApproval> approvals, DateTimeOffset approvedAt)
    {
        var approvalIds = (approvals ?? throw new ArgumentNullException(nameof(approvals)))
            .Where(approval => approval.Decision == ProtocolApprovalDecision.Approved)
            .Select(approval => approval.ApprovalId)
            .ToArray();

        return new ProtocolVersion(
            Id,
            ProtocolId,
            ProjectId,
            VersionNumber,
            ProtocolStatus.Approved,
            Template,
            Intent,
            Values,
            RequiredDecisions,
            Decisions,
            Waivers,
            ContentDigest,
            ApprovalPolicyId,
            approvalIds,
            approvedAt,
            SupersedesVersionId,
            SupersededByVersionId,
            AmendmentId);
    }

    public ProtocolVersion SupersededBy(string successorVersionId)
    {
        return new ProtocolVersion(
            Id,
            ProtocolId,
            ProjectId,
            VersionNumber,
            ProtocolStatus.Superseded,
            Template,
            Intent,
            Values,
            RequiredDecisions,
            Decisions,
            Waivers,
            ContentDigest,
            ApprovalPolicyId,
            ApprovalIds,
            ApprovedAt,
            SupersedesVersionId,
            Guard.NotBlank(successorVersionId, nameof(successorVersionId)),
            AmendmentId);
    }

    public void AddWaiver(ProtocolWaiver _)
    {
        throw new ProtocolRuleException(
            ProtocolErrorCodes.PostApprovalMutation,
            "Approved protocol versions cannot be mutated.");
    }

    public DigestEnvelope ToProtocolContentDigestEnvelope()
    {
        return new DigestEnvelope(
            DigestScope.ProtocolContent,
            "nexus.protocol-content",
            "1.0.0",
            ProtocolDigestMaterial.Build(
                ProtocolId,
                Id,
                ProjectId,
                VersionNumber,
                Template,
                Intent,
                Values,
                RequiredDecisions,
                Decisions,
                Waivers,
                SupersedesVersionId,
                AmendmentId));
    }
}

public sealed record ProtocolWaiver(
    string WaiverId,
    string AffectedRequirementId,
    string? Condition,
    DateTimeOffset? ExpiresAt,
    string Rationale,
    string ConsequenceWarning,
    string DisclosureMapping,
    ActorId RequestedBy,
    DateTimeOffset RequestedAt,
    string ApprovalPolicyId,
    IReadOnlyList<string> ApprovalIds)
{
    public IReadOnlyList<string> ApprovalIds { get; } = (ApprovalIds ?? Array.Empty<string>()).ToArray();

    public CanonicalJsonObject ToCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .Add("affected_requirement_id", Guard.NotBlank(AffectedRequirementId, nameof(AffectedRequirementId)))
            .Add("approval_ids", CanonicalJsonValue.Array(ApprovalIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("approval_policy_id", Guard.NotBlank(ApprovalPolicyId, nameof(ApprovalPolicyId)))
            .Add("consequence_warning", Guard.NotBlank(ConsequenceWarning, nameof(ConsequenceWarning)))
            .Add("disclosure_mapping", Guard.NotBlank(DisclosureMapping, nameof(DisclosureMapping)))
            .Add("rationale", Guard.NotBlank(Rationale, nameof(Rationale)))
            .AddTimestamp("requested_at", RequestedAt)
            .Add("requested_by", RequestedBy.ToString())
            .Add("waiver_id", Guard.NotBlank(WaiverId, nameof(WaiverId)));

        if (Condition is not null)
        {
            result.Add("condition", Condition);
        }

        if (ExpiresAt is not null)
        {
            result.AddTimestamp("expires_at", ExpiresAt.Value);
        }

        return result;
    }
}

public sealed record ProtocolDeviation(
    string DeviationId,
    string ProtocolVersionId,
    string PlannedRequirementId,
    string ActualConductSummary,
    string Rationale,
    string Classification,
    ActorId RecordedBy,
    DateTimeOffset RecordedAt,
    string Effect,
    string DisclosureMapping)
{
    public static ProtocolDeviation Record(
        IIdGenerator ids,
        ProtocolVersion version,
        string plannedRequirementId,
        string actualConductSummary,
        string rationale,
        string classification,
        ProtocolActor recordedBy,
        IClock clock,
        string effect,
        string disclosureMapping)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(clock);

        if (!recordedBy.IsHuman)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.InvalidDeviation,
                "Protocol deviations must identify a human recorder.");
        }

        return new ProtocolDeviation(
            ids.NewId().ToString("D"),
            version.Id,
            Guard.NotBlank(plannedRequirementId, nameof(plannedRequirementId)),
            Guard.NotBlank(actualConductSummary, nameof(actualConductSummary)),
            Guard.NotBlank(rationale, nameof(rationale)),
            Guard.NotBlank(classification, nameof(classification)),
            recordedBy.Id,
            clock.UtcNow,
            Guard.NotBlank(effect, nameof(effect)),
            Guard.NotBlank(disclosureMapping, nameof(disclosureMapping)));
    }
}

public sealed record ProtocolInvalidationNotice(
    string NoticeId,
    string SourceAmendmentId,
    string AffectedRequirementId,
    ContentDigest AffectedArtifactDigest,
    string AffectedWorkflowNodeId,
    string Effect,
    string RequiredAction,
    DateTimeOffset CreatedAt)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("affected_artifact_digest", AffectedArtifactDigest.ToString())
            .Add("affected_requirement_id", Guard.NotBlank(AffectedRequirementId, nameof(AffectedRequirementId)))
            .Add("affected_workflow_node_id", Guard.NotBlank(AffectedWorkflowNodeId, nameof(AffectedWorkflowNodeId)))
            .AddTimestamp("created_at", CreatedAt)
            .Add("effect", Guard.NotBlank(Effect, nameof(Effect)))
            .Add("notice_id", Guard.NotBlank(NoticeId, nameof(NoticeId)))
            .Add("required_action", Guard.NotBlank(RequiredAction, nameof(RequiredAction)))
            .Add("source_amendment_id", Guard.NotBlank(SourceAmendmentId, nameof(SourceAmendmentId)));
    }
}

public sealed record ProtocolAmendment(
    string AmendmentId,
    string ProtocolId,
    string AmendsVersionId,
    string ProducesVersionId,
    ContentDigest PreviousContentDigest,
    ActorId RequestedBy,
    DateTimeOffset RequestedAt,
    string Rationale,
    IReadOnlyList<string> ChangedDecisionKeys,
    IReadOnlyList<ProtocolInvalidationNotice> InvalidationNotices,
    ContentDigest? InvalidationPlanDigest,
    string ApprovalPolicyId,
    IReadOnlyList<string> ApprovalIds)
{
    public static ProtocolAmendment Create(
        IIdGenerator ids,
        ProtocolVersion previousVersion,
        string producesVersionId,
        ProtocolActor requestedBy,
        IClock clock,
        string rationale,
        IEnumerable<string> changedDecisionKeys,
        IEnumerable<ProtocolInvalidationNotice> invalidationNotices,
        ApprovalPolicy policy,
        ContentDigest? invalidationPlanDigest = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(previousVersion);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(policy);

        var changedKeys = (changedDecisionKeys ?? throw new ArgumentNullException(nameof(changedDecisionKeys)))
            .Select(key => Guard.NotBlank(key, nameof(changedDecisionKeys)))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        if (!requestedBy.IsHuman || changedKeys.Length == 0)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.InvalidAmendment,
                "A protocol amendment requires a human requester and changed decision keys.");
        }

        return new ProtocolAmendment(
            ids.NewId().ToString("D"),
            previousVersion.ProtocolId,
            previousVersion.Id,
            Guard.NotBlank(producesVersionId, nameof(producesVersionId)),
            previousVersion.ContentDigest,
            requestedBy.Id,
            clock.UtcNow,
            Guard.NotBlank(rationale, nameof(rationale)),
            changedKeys,
            (invalidationNotices ?? Array.Empty<ProtocolInvalidationNotice>()).ToArray(),
            invalidationPlanDigest,
            policy.PolicyId,
            Array.Empty<string>());
    }

    public IReadOnlyList<string> ChangedDecisionKeys { get; } = (ChangedDecisionKeys ?? Array.Empty<string>())
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<ProtocolInvalidationNotice> InvalidationNotices { get; } =
        (InvalidationNotices ?? Array.Empty<ProtocolInvalidationNotice>()).ToArray();

    public IReadOnlyList<string> ApprovalIds { get; } = (ApprovalIds ?? Array.Empty<string>()).ToArray();
}

internal static class ProtocolDigestMaterial
{
    public static CanonicalJsonObject Build(
        string protocolId,
        string versionId,
        string projectId,
        int versionNumber,
        ProtocolTemplate template,
        ProtocolIntent intent,
        CanonicalJsonObject values,
        IEnumerable<RequiredDecisionDefinition> requiredDecisions,
        IEnumerable<ProtocolDecision> decisions,
        IEnumerable<ProtocolWaiver> waivers,
        string? supersedesVersionId,
        string? amendmentId)
    {
        var result = new CanonicalJsonObject()
            .Add("decisions", CanonicalJsonValue.Array(
                decisions
                    .OrderBy(decision => decision.DecisionKey, StringComparer.Ordinal)
                    .ThenBy(decision => decision.DecisionId, StringComparer.Ordinal)
                    .Select(decision => decision.ToCanonicalJson())
                    .ToArray()))
            .Add("intent", intent.ToCanonicalJson())
            .Add("project_id", Guard.NotBlank(projectId, nameof(projectId)))
            .Add("protocol_id", Guard.NotBlank(protocolId, nameof(protocolId)))
            .Add("required_decisions", CanonicalJsonValue.Array(
                requiredDecisions
                    .OrderBy(decision => decision.DecisionKey, StringComparer.Ordinal)
                    .Select(decision => decision.ToCanonicalJson())
                    .ToArray()))
            .Add("template", template.ToCanonicalJson())
            .Add("values", (CanonicalJsonObject)CanonicalJsonValue.DeepClone(values))
            .Add("version_id", Guard.NotBlank(versionId, nameof(versionId)))
            .Add("version_number", versionNumber)
            .Add("waivers", CanonicalJsonValue.Array(
                waivers
                    .OrderBy(waiver => waiver.AffectedRequirementId, StringComparer.Ordinal)
                    .ThenBy(waiver => waiver.WaiverId, StringComparer.Ordinal)
                    .Select(waiver => waiver.ToCanonicalJson())
                    .ToArray()));

        if (supersedesVersionId is not null)
        {
            result.Add("supersedes_version_id", supersedesVersionId);
        }

        if (amendmentId is not null)
        {
            result.Add("amendment_id", amendmentId);
        }

        return result;
    }
}
