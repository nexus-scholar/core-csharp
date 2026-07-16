using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public static class ProtocolDeviationConstants
{
    public const string SchemaId = "nexus.protocol.deviation";
    public const string SchemaVersion = "1.0.0";
    public const string ApprovedAmendmentRequired = "approved_amendment_required";
    public const string Deviation = "protocol_deviation";
    public const string OperationalVariance = "operational_variance_no_scientific_effect";
    public const string UnresolvedInconsistency = "unresolved_inconsistency";
    public static bool IsClassification(string value) => value is ApprovedAmendmentRequired or Deviation or OperationalVariance or UnresolvedInconsistency;
}

public sealed record ProtocolDeviationEvidenceReference(string Kind, string Id, ContentDigest Digest);
public sealed record ProtocolDeviationInvalidationEffect(string TargetKind, string TargetId, ContentDigest TargetDigest, string RequiredAction);

public sealed record ProtocolDeviationRecord(
    string DeviationId,
    string ProtocolId,
    string ProtocolVersionId,
    ContentDigest ProtocolContentDigest,
    string PlannedRequirementId,
    string? ProfileId,
    ContentDigest? ProfileDigest,
    string? ShortcutId,
    string ActualConduct,
    string Rationale,
    string Classification,
    string Consequence,
    string MitigationApplied,
    IReadOnlyList<ProtocolDeviationEvidenceReference> MitigationEvidenceReferences,
    string Effect,
    string Disclosure,
    ActorId RecordedBy,
    DateTimeOffset RecordedAt,
    string ApprovalPolicyId,
    IReadOnlyList<string> ApprovalIds,
    IReadOnlyList<ProtocolDeviationInvalidationEffect> InvalidationEffects,
    string? SuccessorAmendmentId)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        var value = new CanonicalJsonObject().Add("deviation_id", DeviationId).Add("protocol_id", ProtocolId)
            .Add("protocol_version_id", ProtocolVersionId).Add("protocol_content_digest", ProtocolContentDigest.ToString())
            .Add("planned_requirement_id", PlannedRequirementId).Add("actual_conduct", ActualConduct).Add("rationale", Rationale)
            .Add("classification", Classification).Add("consequence", Consequence).Add("mitigation_applied", MitigationApplied)
            .Add("mitigation_evidence_refs", CanonicalJsonValue.Array(MitigationEvidenceReferences.Select(item =>
                new CanonicalJsonObject().Add("kind", item.Kind).Add("id", item.Id).Add("digest", item.Digest.ToString())).ToArray()))
            .Add("effect", Effect).Add("disclosure", Disclosure).Add("recorded_by", RecordedBy.ToString())
            .AddTimestamp("recorded_at", RecordedAt).Add("approval_policy_id", ApprovalPolicyId)
            .Add("approval_ids", CanonicalJsonValue.Array(ApprovalIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("invalidation_effects", CanonicalJsonValue.Array(InvalidationEffects.Select(item =>
                new CanonicalJsonObject().Add("target_kind", item.TargetKind).Add("target_id", item.TargetId)
                    .Add("target_digest", item.TargetDigest.ToString()).Add("required_action", item.RequiredAction)).ToArray()));
        if (ProfileId is not null)
            value.Add("profile_id", ProfileId).Add("profile_digest", ProfileDigest!.Value.ToString()).Add("shortcut_id", ShortcutId!);
        if (SuccessorAmendmentId is not null) value.Add("successor_amendment_id", SuccessorAmendmentId);
        return value;
    }
}

public sealed record UnverifiedProtocolDeviation(ProtocolDeviationRecord Deviation, ContentDigest DeviationDigest);

public interface IProtocolDeviationAuthorityResolver : IProtocolSupplementalAuthorityResolver
{
    VerifiedProtocolAmendment ResolveProtocolAmendment(string amendmentId);
}

public sealed class VerifiedProtocolDeviation
{
    internal VerifiedProtocolDeviation(ProtocolDeviationRecord deviation, ContentDigest digest, VerifiedProtocolVersion protocol,
        ApprovalPolicy policy, IReadOnlyList<VerifiedProtocolSupplementalApproval> approvals, VerifiedProtocolAmendment? successor)
    {
        Deviation = deviation with
        {
            MitigationEvidenceReferences = Array.AsReadOnly(deviation.MitigationEvidenceReferences.Select(item => item with { }).ToArray()),
            ApprovalIds = Array.AsReadOnly(deviation.ApprovalIds.ToArray()),
            InvalidationEffects = Array.AsReadOnly(deviation.InvalidationEffects.Select(item => item with { }).ToArray())
        };
        DeviationDigest = digest; ProtocolVersion = protocol; Policy = policy;
        Approvals = Array.AsReadOnly(approvals.ToArray()); SuccessorAmendment = successor;
    }
    public ProtocolDeviationRecord Deviation { get; }
    public ContentDigest DeviationDigest { get; }
    public VerifiedProtocolVersion ProtocolVersion { get; }
    public ApprovalPolicy Policy { get; }
    public IReadOnlyList<VerifiedProtocolSupplementalApproval> Approvals { get; }
    public VerifiedProtocolAmendment? SuccessorAmendment { get; }
    public bool BlocksFinalReporting => Deviation.Classification == ProtocolDeviationConstants.UnresolvedInconsistency;
}

public static partial class ProtocolSupplementalAuthorityRehydrator
{
    public static VerifiedProtocolDeviation RehydrateDeviation(UnverifiedProtocolDeviation input, IProtocolDeviationAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resolver);
        var value = Normalize(input.Deviation, resolver);
        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ProtocolDeviationConstants.SchemaId,
            ProtocolDeviationConstants.SchemaVersion, value.ToCanonicalJson());
        var digest = envelope.ComputeDigest();
        if (digest != input.DeviationDigest) throw Rule("Deviation digest does not reproduce from canonical content.");
        var protocol = resolver.ResolveProtocolVersion(value.ProtocolVersionId) ?? throw Rule("Deviation Protocol authority could not be resolved.");
        if (protocol.Version.Status != ProtocolStatus.Approved || protocol.Version.ProtocolId != value.ProtocolId ||
            protocol.Version.Id != value.ProtocolVersionId || protocol.Version.ContentDigest != value.ProtocolContentDigest)
            throw Rule("Deviation does not bind the exact approved Protocol authority.");
        var policy = resolver.ResolvePolicy(ProtocolSupplementalTargetTypes.Deviation, value.DeviationId) ?? throw Rule("Deviation approval policy could not be resolved.");
        if (policy.PolicyId != value.ApprovalPolicyId || policy.AllowsAutomation) throw Rule("Deviation approval policy is mismatched or permits automation.");
        var approvals = ResolveAndValidateApprovals(value.ApprovalIds, ProtocolSupplementalTargetTypes.Deviation,
            value.DeviationId, digest, policy, resolver);
        VerifiedProtocolAmendment? successor = null;
        if (value.Classification == ProtocolDeviationConstants.ApprovedAmendmentRequired)
        {
            if (value.SuccessorAmendmentId is null) throw Rule("Amendment-required deviation must identify its successor amendment.");
            successor = resolver.ResolveProtocolAmendment(value.SuccessorAmendmentId) ?? throw Rule("Successor amendment could not be resolved.");
            if (successor.PreviousVersion.Version.Id != value.ProtocolVersionId ||
                successor.PreviousVersion.Version.ContentDigest != value.ProtocolContentDigest)
                throw Rule("Successor amendment does not amend the deviated Protocol authority.");
        }
        else if (value.SuccessorAmendmentId is not null) throw Rule("Only amendment-required deviations may identify a successor amendment.");
        return new VerifiedProtocolDeviation(value, digest, protocol, policy, approvals, successor);
    }

    private static ProtocolDeviationRecord Normalize(ProtocolDeviationRecord source, IProtocolDeviationAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!ProtocolDeviationConstants.IsClassification(source.Classification) || source.RecordedAt.Offset != TimeSpan.Zero ||
            !resolver.IsHumanActor(source.RecordedBy) || !source.ProtocolContentDigest.IsValid)
            throw Rule("Deviation classification, recorder, time, or Protocol digest is invalid.");
        var profileParts = new object?[] { source.ProfileId, source.ProfileDigest, source.ShortcutId };
        if (profileParts.Any(item => item is not null) && profileParts.Any(item => item is null) || source.ProfileDigest is { IsValid: false })
            throw Rule("Deviation profile and shortcut binding must be complete.");
        var evidenceSource = source.MitigationEvidenceReferences ?? throw Rule("Deviation mitigation evidence is required.");
        var effectsSource = source.InvalidationEffects ?? throw Rule("Deviation invalidation effects are required.");
        var approvalSource = source.ApprovalIds ?? throw Rule("Deviation approval ids are required.");
        var evidence = evidenceSource.Select(item => item with
        {
            Kind = Required(item.Kind),
            Id = Required(item.Id)
        }).OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var effects = effectsSource.Select(item => item with
        {
            TargetKind = Required(item.TargetKind),
            TargetId = Required(item.TargetId),
            RequiredAction = Required(item.RequiredAction)
        }).OrderBy(item => item.TargetKind, StringComparer.Ordinal).ThenBy(item => item.TargetId, StringComparer.Ordinal).ToArray();
        if (evidence.Length == 0 || evidence.Any(item => !item.Digest.IsValid) || evidence.Select(item => (item.Kind, item.Id)).Distinct().Count() != evidence.Length ||
            effects.Length == 0 || effects.Any(item => !item.TargetDigest.IsValid) || effects.Select(item => (item.TargetKind, item.TargetId)).Distinct().Count() != effects.Length)
            throw Rule("Deviation mitigation evidence and invalidation effects must be complete, unique, and digest-bound.");
        var approvals = approvalSource.Select(Required).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (!evidenceSource.Select(item => (item.Kind.Trim(), item.Id.Trim())).SequenceEqual(evidence.Select(item => (item.Kind, item.Id))) ||
            !effectsSource.Select(item => (item.TargetKind.Trim(), item.TargetId.Trim())).SequenceEqual(effects.Select(item => (item.TargetKind, item.TargetId))) ||
            !approvalSource.Select(item => item.Trim()).SequenceEqual(approvals))
            throw Rule("Deviation collections must use canonical ordinal order.");
        if (approvals.Length == 0 || approvals.Distinct(StringComparer.Ordinal).Count() != approvals.Length) throw Rule("Deviation approval ids must be unique and present.");
        return source with
        {
            DeviationId = Required(source.DeviationId),
            ProtocolId = Required(source.ProtocolId),
            ProtocolVersionId = Required(source.ProtocolVersionId),
            ProfileId = source.ProfileId is null ? null : Required(source.ProfileId),
            ShortcutId = source.ShortcutId is null ? null : Required(source.ShortcutId),
            PlannedRequirementId = Required(source.PlannedRequirementId),
            ActualConduct = Required(source.ActualConduct),
            Rationale = Required(source.Rationale),
            Consequence = Required(source.Consequence),
            MitigationApplied = Required(source.MitigationApplied),
            Effect = Required(source.Effect),
            Disclosure = Required(source.Disclosure),
            ApprovalPolicyId = Required(source.ApprovalPolicyId),
            SuccessorAmendmentId = source.SuccessorAmendmentId is null ? null : Required(source.SuccessorAmendmentId),
            MitigationEvidenceReferences = Array.AsReadOnly(evidence),
            ApprovalIds = Array.AsReadOnly(approvals),
            InvalidationEffects = Array.AsReadOnly(effects)
        };
    }

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw Rule("Deviation field is required.");
    private static ProtocolRuleException Rule(string message) => new(ProtocolErrorCodes.InvalidDeviation, message);
}
