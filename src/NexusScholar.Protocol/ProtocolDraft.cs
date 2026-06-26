using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public sealed class ProtocolDraft
{
    private readonly Dictionary<string, ProtocolDecision> _decisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnresolvedDecision> _unresolvedDecisions = new(StringComparer.Ordinal);
    private readonly List<ProtocolWaiver> _waivers = new();
    private readonly RequiredDecisionDefinition[] _requiredDecisions;
    private const string ProtocolVersionTargetType = "protocol-version";

    private ProtocolDraft(
        string protocolId,
        string draftId,
        string projectId,
        ProtocolTemplate template,
        ProtocolIntent intent,
        CanonicalJsonObject values,
        IEnumerable<RequiredDecisionDefinition> requiredDecisions,
        ProtocolActor createdBy,
        DateTimeOffset createdAt,
        string? baseVersionId)
    {
        ProtocolId = Guard.NotBlank(protocolId, nameof(protocolId));
        DraftId = Guard.NotBlank(draftId, nameof(draftId));
        ProjectId = Guard.NotBlank(projectId, nameof(projectId));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        Values = ((CanonicalJsonObject)CanonicalJsonValue.DeepClone(values ?? throw new ArgumentNullException(nameof(values)))).Freeze();
        _requiredDecisions = (requiredDecisions ?? throw new ArgumentNullException(nameof(requiredDecisions))).ToArray();
        CreatedBy = createdBy.Id;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        BaseVersionId = baseVersionId;
    }

    public string Id => ProtocolId;

    public string ProtocolId { get; }

    public string DraftId { get; }

    public string ProjectId { get; }

    public ProtocolStatus Status { get; private set; } = ProtocolStatus.Draft;

    public ProtocolTemplate Template { get; }

    public ProtocolIntent Intent { get; }

    public CanonicalJsonObject Values { get; }

    public IReadOnlyList<RequiredDecisionDefinition> RequiredDecisions => _requiredDecisions.ToArray();

    public IReadOnlyCollection<ProtocolDecision> Decisions => _decisions.Values.ToArray();

    public IReadOnlyCollection<UnresolvedDecision> UnresolvedDecisions => _unresolvedDecisions.Values.ToArray();

    public IReadOnlyList<ProtocolWaiver> Waivers => _waivers.ToArray();

    public ActorId CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? BaseVersionId { get; }

    public static ProtocolDraft Create(
        IIdGenerator ids,
        string projectId,
        ProtocolTemplate template,
        ProtocolIntent intent,
        CanonicalJsonObject values,
        IEnumerable<RequiredDecisionDefinition> requiredDecisions,
        ProtocolActor createdBy,
        IClock clock,
        string? baseVersionId = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(clock);

        return new ProtocolDraft(
            NewId(ids),
            NewId(ids),
            projectId,
            template,
            intent,
            values,
            requiredDecisions,
            createdBy,
            clock.UtcNow,
            baseVersionId);
    }

    public static ProtocolDraft Create(
        IIdGenerator ids,
        string title,
        IEnumerable<string> requiredDecisionKeys)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(requiredDecisionKeys);

        return new ProtocolDraft(
            NewId(ids),
            NewId(ids),
            "local-project",
            new ProtocolTemplate(
                "legacy-local-template",
                "1.0.0",
                ContentDigest.Sha256Utf8("legacy-local-template")),
            new ProtocolIntent(title, "legacy local review"),
            new CanonicalJsonObject(),
            requiredDecisionKeys.Select(key => new RequiredDecisionDefinition(
                key,
                key,
                string.Empty,
                new CanonicalJsonObject().Add("type", "string"),
                "protocol-approval",
                "protocol-approval",
                key,
                false)),
            ProtocolActor.Human("legacy-local-creator"),
            DateTimeOffset.UnixEpoch,
            null);
    }

    public ProtocolDecision RecordDecision(
        IIdGenerator ids,
        string decisionKey,
        CanonicalJsonValue value,
        ProtocolActor actor,
        IClock clock,
        string? rationale = null,
        ContentDigest? sourceProposalDigest = null,
        string? supersedesDecisionId = null)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureDecisionActor(actor);

        decisionKey = Guard.NotBlank(decisionKey, nameof(decisionKey));

        if (_decisions.ContainsKey(decisionKey))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.DuplicateDecision,
                $"Decision '{decisionKey}' already exists. Create an explicit revision instead of overwriting it.");
        }

        var decision = new ProtocolDecision(
            NewId(ids),
            decisionKey,
            CanonicalJsonValue.DeepClone(value),
            rationale,
            actor.Id,
            clock.UtcNow,
            sourceProposalDigest,
            supersedesDecisionId);
        _decisions.Add(decisionKey, decision);
        UpdatedAt = clock.UtcNow;
        return decision;
    }

    public ProtocolDecision RecordDecision(
        string key,
        string value,
        ActorId actor,
        IClock clock)
    {
        var deterministicId = new DeterministicSingleIdGenerator(ContentDigest.Sha256Utf8($"{ProtocolId}:{key}:{_decisions.Count}").Value);
        return RecordDecision(
            deterministicId,
            key,
            CanonicalJsonValue.From(Guard.NotBlank(value, nameof(value))),
            ProtocolActor.Human(actor),
            clock);
    }

    public UnresolvedDecision AddUnresolvedDecision(
        IIdGenerator ids,
        string decisionKey,
        string question,
        string reason,
        string requiredBefore,
        ProtocolActor createdBy,
        IClock clock,
        bool blocksProtocolApproval)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureDecisionActor(createdBy);

        if (!_requiredDecisions.Any(required => string.Equals(required.DecisionKey, decisionKey, StringComparison.Ordinal)))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.UnauthorizedApproval,
                "Unresolved decision key is not declared in required decisions.");
        }

        var unresolved = new UnresolvedDecision(
            NewId(ids),
            Guard.NotBlank(decisionKey, nameof(decisionKey)),
            Guard.NotBlank(question, nameof(question)),
            Guard.NotBlank(reason, nameof(reason)),
            Guard.NotBlank(requiredBefore, nameof(requiredBefore)),
            createdBy.Id,
            clock.UtcNow,
            blocksProtocolApproval);
        _unresolvedDecisions.Add(unresolved.UnresolvedId, unresolved);
        UpdatedAt = clock.UtcNow;
        return unresolved;
    }

    public ProtocolWaiver AddWaiver(
        IIdGenerator ids,
        string affectedRequirementId,
        string? condition,
        DateTimeOffset? expiresAt,
        string rationale,
        string consequenceWarning,
        string disclosureMapping,
        ProtocolActor requestedBy,
        IClock clock,
        ApprovalPolicy policy,
        IEnumerable<string>? approvalIds = null)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(policy);

        if (!requestedBy.IsHuman)
        {
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidWaiver, "Protocol waivers must be requested by a human actor.");
        }

        var targetExists = _requiredDecisions.Any(required =>
            string.Equals(required.DecisionKey, affectedRequirementId, StringComparison.Ordinal) ||
            string.Equals(required.SourceRequirementId, affectedRequirementId, StringComparison.Ordinal));

        if (!targetExists)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.InvalidWaiver,
                "Waiver requirement was not declared in the protocol draft.");
        }

        var targetRequirement = _requiredDecisions.Single(required =>
            string.Equals(required.DecisionKey, affectedRequirementId, StringComparison.Ordinal) ||
            string.Equals(required.SourceRequirementId, affectedRequirementId, StringComparison.Ordinal));

        if (!targetRequirement.AllowsWaiver)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.InvalidWaiver,
                "Waiver was requested for a requirement that cannot be waived.");
        }

        var normalizedApprovalIds = (approvalIds ?? Array.Empty<string>()).Select(id => Guard.NotBlank(id, nameof(approvalIds))).ToArray();

        var waiver = new ProtocolWaiver(
            NewId(ids),
            affectedRequirementId,
            condition,
            expiresAt,
            rationale,
            consequenceWarning,
            disclosureMapping,
            requestedBy.Id,
            clock.UtcNow,
            policy.PolicyId,
            normalizedApprovalIds);
        _waivers.Add(waiver);
        UpdatedAt = clock.UtcNow;
        return waiver;
    }

    public ProtocolVersion CreateApprovalCandidate(
        IIdGenerator ids,
        ApprovalPolicy policy,
        int versionNumber = 1,
        string? versionId = null,
        string? supersedesVersionId = null,
        string? amendmentId = null)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.AllowsAutomation)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.UnauthorizedApproval,
                "Protocol approval policies must not allow automation authority.");
        }

        EnsureNoMissingRequiredDecisions();
        EnsureNoBlockingUnresolvedDecisions();

        versionId ??= NewId(ids);
        var digestMaterial = ProtocolDigestMaterial.Build(
            ProtocolId,
            versionId,
            ProjectId,
            versionNumber,
            Template,
            Intent,
            Values,
            _requiredDecisions,
            _decisions.Values,
            _waivers,
            _unresolvedDecisions.Values,
            supersedesVersionId,
            amendmentId);
        var digest = new DigestEnvelope(
            DigestScope.ProtocolContent,
            "nexus.protocol-content",
            "1.0.0",
            digestMaterial).ComputeDigest();

        return new ProtocolVersion(
            versionId,
            ProtocolId,
            ProjectId,
            versionNumber,
            ProtocolStatus.ReadyForReview,
            Template,
            Intent,
            Values,
            _requiredDecisions,
            _decisions.Values,
            _waivers,
            digest,
            policy.PolicyId,
            Array.Empty<string>(),
            null,
            supersedesVersionId,
            null,
            amendmentId,
            _unresolvedDecisions.Values);
    }

    public ProtocolVersion ApproveCandidate(
        ProtocolVersion candidate,
        ApprovalPolicy policy,
        IEnumerable<ProtocolApproval> approvals,
        IClock clock)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(clock);

        var approvalsToEvaluate = (approvals ?? throw new ArgumentNullException(nameof(approvals))).ToArray();

        EnsureCandidateStillMatchesDraft(candidate);
        var approved = ValidateApprovalPolicy(candidate, policy, approvalsToEvaluate);
        Status = ProtocolStatus.Approved;
        UpdatedAt = clock.UtcNow;
        return candidate.WithApprovals(approved, clock.UtcNow);
    }

    public ProtocolVersion Approve(
        ActorId actor,
        IClock clock,
        IIdGenerator ids)
    {
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(
            ids,
            candidate,
            policy,
            ProtocolActor.Human(actor),
            clock,
            candidate.ContentDigest);
        return ApproveCandidate(candidate, policy, new[] { approval }, clock);
    }

    private void EnsureNoMissingRequiredDecisions()
    {
        var missing = _requiredDecisions
            .Where(required => !_decisions.ContainsKey(required.DecisionKey) &&
                !_unresolvedDecisions.Values.Any(unresolved =>
                    string.Equals(unresolved.DecisionKey, required.DecisionKey, StringComparison.Ordinal) &&
                    required.AllowsUnresolved))
            .Select(required => required.DecisionKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.MissingRequiredDecision,
                $"Protocol cannot be approved. Missing decisions: {string.Join(", ", missing)}.");
        }
    }

    private void EnsureNoBlockingUnresolvedDecisions()
    {
        if (_unresolvedDecisions.Values.Any(unresolved => unresolved.BlocksProtocolApproval))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.BlockingUnresolvedDecision,
                "Protocol cannot be approved while blocking unresolved decisions remain.");
        }
    }

    private static ProtocolApproval[] ValidateApprovalPolicy(
        ProtocolVersion candidate,
        ApprovalPolicy policy,
        IReadOnlyList<ProtocolApproval> approvals)
    {
        var validatedApprovals = approvals
            .Select(approval => ValidateApprovalRecord(candidate, policy, approval))
            .ToArray();

        var supersededApprovalIds = validatedApprovals
            .Where(approval => !string.IsNullOrWhiteSpace(approval.SupersedesApprovalId))
            .Select(approval => approval.SupersedesApprovalId!)
            .ToHashSet(StringComparer.Ordinal);

        var matching = validatedApprovals
            .Where(approval =>
                approval.Decision == ProtocolApprovalDecision.Approved &&
                string.IsNullOrWhiteSpace(approval.SupersedesApprovalId) &&
                !supersededApprovalIds.Contains(approval.ApprovalId))
            .GroupBy(approval => approval.ApprovalId)
            .Select(group => group.First())
            .ToArray();

        if (policy.AllowsAutomation)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.UnauthorizedApproval,
                "Protocol approval policies must not allow automation authority.");
        }

        if (candidate.Status != ProtocolStatus.ReadyForReview)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Protocol can only be approved from a review candidate.");
        }

        switch (policy.Mode)
        {
            case ApprovalPolicyMode.DualIndependent:
                if (matching.Length < policy.MinimumApprovals)
                {
                    throw new ProtocolRuleException(
                        ProtocolErrorCodes.InsufficientApprovalPolicy,
                        "Dual-independent approval policy did not receive enough approvals.");
                }
                break;
            default:
                if (matching.Length < policy.MinimumApprovals)
                {
                    throw new ProtocolRuleException(
                        ProtocolErrorCodes.InsufficientApprovalPolicy,
                        "Insufficient approvals for the selected protocol approval policy.");
                }
                break;
        }

        if (policy.Mode == ApprovalPolicyMode.DualIndependent &&
            matching.Select(approval => approval.ApprovedBy).Distinct().Count() != matching.Length)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.SameActorDualApproval,
                "Dual-independent protocol approval requires distinct actors.");
        }

        if (policy.RequiresDistinctActors &&
            matching.Select(approval => approval.ApprovedBy).Distinct().Count() != matching.Length)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.SameActorDualApproval,
                "Protocol approval requires distinct actors.");
        }

        foreach (var requiredRole in policy.RequiredRoles.Where(role => role.Length > 0).Distinct(StringComparer.Ordinal))
        {
            if (!matching.Any(approval => string.Equals(approval.Role, requiredRole, StringComparison.Ordinal)))
            {
                throw new ProtocolRuleException(
                    ProtocolErrorCodes.InsufficientApprovalPolicy,
                    $"Approval policy requires role '{requiredRole}' that was not provided.");
            }
        }

        if (matching.Length > 0 &&
            matching.Any(approval => !string.Equals(approval.TargetType, ProtocolVersionTargetType, StringComparison.Ordinal)))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "Approval target type does not match the protocol-version aggregate.");
        }

        return matching;
    }

    private static ProtocolApproval ValidateApprovalRecord(
        ProtocolVersion candidate,
        ApprovalPolicy policy,
        ProtocolApproval approval)
    {
        if (!approval.HasValidApprovalRecordDigest())
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "Approval record digest is invalid.");
        }

        if (approval.ApprovedBy.Value.Length == 0)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.MissingApprovalActor,
                "Approval actor is required.");
        }

        if (!approval.IsApprovedByHuman())
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.NonHumanApprovalActor,
                "Approval actor must be a human.");
        }

        if (!approval.IsBoundToTarget(candidate, policy))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.ApprovalTargetMismatch,
                "Approval target did not match candidate version, policy, or protocol content.");
        }

        return approval;
    }

    private static void EnsureDecisionActor(ProtocolActor actor)
    {
        if (actor.Id.Value.Length == 0)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.MissingApprovalActor,
                "Protocol decision actor is required.");
        }

        if (!actor.IsHuman)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.NonHumanApprovalActor,
                "Protocol decisions cannot be recorded by automated actors.");
        }
    }

    private void EnsureCandidateStillMatchesDraft(ProtocolVersion candidate)
    {
        if (!string.Equals(candidate.ProtocolId, ProtocolId, StringComparison.Ordinal) ||
            !string.Equals(candidate.ProjectId, ProjectId, StringComparison.Ordinal))
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Approval candidate does not match the current draft identity.");
        }

        var currentDigest = new DigestEnvelope(
            DigestScope.ProtocolContent,
            "nexus.protocol-content",
            "1.0.0",
            ProtocolDigestMaterial.Build(
                ProtocolId,
                candidate.Id,
                ProjectId,
                candidate.VersionNumber,
                Template,
                Intent,
                Values,
                _requiredDecisions,
                _decisions.Values,
                _waivers,
                _unresolvedDecisions.Values,
                candidate.SupersedesVersionId,
                candidate.AmendmentId)).ComputeDigest();

        if (currentDigest != candidate.ContentDigest)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.StaleContentDigest,
                "Approval candidate digest is stale because the draft changed after digest computation.");
        }
    }

    private void EnsureDraft()
    {
        if (Status != ProtocolStatus.Draft && Status != ProtocolStatus.ReadyForReview)
        {
            throw new ProtocolRuleException(
                ProtocolErrorCodes.PostApprovalMutation,
                "Only a draft protocol can be changed or approved.");
        }
    }

    private static string NewId(IIdGenerator ids) => ids.NewId().ToString("D");

    private sealed class DeterministicSingleIdGenerator : IIdGenerator
    {
        private readonly Guid _id;

        public DeterministicSingleIdGenerator(string digestValue)
        {
            _id = new Guid(digestValue[..32]);
        }

        public Guid NewId() => _id;
    }
}
