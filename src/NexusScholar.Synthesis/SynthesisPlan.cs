using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Synthesis;

public static class SynthesisSchemas
{
    public const string Plan = "nexus.synthesis.plan";
    public const string Invalidation = "nexus.synthesis.invalidation";
    public const string Version = "1.0.0";
}

public static class SynthesisErrorCodes
{
    public const string InvalidAuthority = "invalid-synthesis-authority";
    public const string StaleSource = "stale-synthesis-source";
    public const string IneligibleSource = "ineligible-synthesis-source";
    public const string MeasureMismatch = "synthesis-effect-measure-mismatch";
    public const string AutomationCannotAuthorize = "automation-cannot-authorize-synthesis";
}

public sealed class SynthesisRuleException : InvalidOperationException
{
    public SynthesisRuleException(string category, string message) : base(message) => Category = category;
    public string Category { get; }
}

public static class SynthesisActorKinds
{
    public const string Human = "human";
    public const string Automation = "automation";
}

public sealed record SynthesisActor(string ActorId, string Kind, string Role)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        var kind = Required(Kind, nameof(Kind)).ToLowerInvariant();
        if (kind is not SynthesisActorKinds.Human and not SynthesisActorKinds.Automation)
            throw Rule(SynthesisErrorCodes.InvalidAuthority, "Unknown synthesis actor kind.");
        return new CanonicalJsonObject().Add("actor_id", Required(ActorId, nameof(ActorId)))
            .Add("kind", kind).Add("role", Required(Role, nameof(Role)));
    }

    internal bool IsHuman => string.Equals(Kind, SynthesisActorKinds.Human, StringComparison.OrdinalIgnoreCase);
    private static string Required(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException("Value is required.", name);
    private static SynthesisRuleException Rule(string category, string message) => new(category, message);
}

public sealed record SynthesisOutcome(
    string OutcomeId,
    string Name,
    string EffectMeasure,
    string Unit,
    string Timepoint)
{
    internal CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("outcome_id", Required(OutcomeId)).Add("name", Required(Name))
        .Add("effect_measure", Required(EffectMeasure)).Add("unit", Required(Unit))
        .Add("timepoint", Required(Timepoint));

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Outcome fields are required.");
}

public sealed record SynthesisTransformation(
    string TransformationId,
    string OutcomeId,
    string FromEffectMeasure,
    string FromUnit,
    string ToEffectMeasure,
    string ToUnit,
    string Method,
    string Rationale)
{
    internal CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("transformation_id", Required(TransformationId)).Add("outcome_id", Required(OutcomeId))
        .Add("from_effect_measure", Required(FromEffectMeasure)).Add("from_unit", Required(FromUnit))
        .Add("to_effect_measure", Required(ToEffectMeasure)).Add("to_unit", Required(ToUnit))
        .Add("method", Required(Method)).Add("rationale", Required(Rationale));

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Transformation fields are required.");
}

public sealed class SynthesisCalculationDeclaration
{
    public SynthesisCalculationDeclaration(string libraryId, string libraryVersion, CanonicalJsonObject configuration)
    {
        LibraryId = Required(libraryId);
        LibraryVersion = Required(libraryVersion);
        Configuration = (CanonicalJsonObject)CanonicalJsonValue.DeepClone(configuration ?? throw new ArgumentNullException(nameof(configuration)));
        Configuration.Freeze();
        ConfigurationDigest = ContentDigest.Sha256CanonicalJson(Configuration);
    }

    public string LibraryId { get; }
    public string LibraryVersion { get; }
    public CanonicalJsonObject Configuration { get; }
    public ContentDigest ConfigurationDigest { get; }

    internal CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("library_id", LibraryId).Add("library_version", LibraryVersion)
        .Add("configuration", Configuration).Add("configuration_digest", ConfigurationDigest.ToString());

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Calculation library id and version are required.");
}

public sealed partial class SynthesisEligibleRecord
{
    internal SynthesisEligibleRecord(
        string recordKind,
        string recordId,
        ContentDigest recordDigest,
        string candidateId,
        string protocolVersionId,
        ContentDigest protocolContentDigest,
        bool isCurrent,
        bool isInvalidated)
    {
        RecordKind = Required(recordKind); RecordId = Required(recordId);
        RecordDigest = recordDigest.IsValid ? recordDigest : throw Rule(SynthesisErrorCodes.InvalidAuthority, "Source record digest is required.");
        CandidateId = Required(candidateId); ProtocolVersionId = Required(protocolVersionId);
        ProtocolContentDigest = protocolContentDigest.IsValid ? protocolContentDigest : throw Rule(SynthesisErrorCodes.InvalidAuthority, "Source Protocol digest is required.");
        IsCurrent = isCurrent; IsInvalidated = isInvalidated;
    }

    public string RecordKind { get; }
    public string RecordId { get; }
    public ContentDigest RecordDigest { get; }
    public string CandidateId { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public bool IsCurrent { get; }
    public bool IsInvalidated { get; }

    internal CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("record_kind", RecordKind).Add("record_id", RecordId).Add("record_digest", RecordDigest.ToString())
        .Add("candidate_id", CandidateId).Add("protocol_version_id", ProtocolVersionId)
        .Add("protocol_content_digest", ProtocolContentDigest.ToString()).Add("is_current", IsCurrent).Add("is_invalidated", IsInvalidated);

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException("Value is required.");
    private static SynthesisRuleException Rule(string category, string message) => new(category, message);
}

public sealed record SynthesisSourceOutcome(
    ContentDigest SourceRecordDigest,
    string OutcomeId,
    string EffectMeasure,
    string Unit)
{
    internal CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("source_record_digest", SourceRecordDigest.ToString()).Add("outcome_id", OutcomeId)
        .Add("effect_measure", EffectMeasure).Add("unit", Unit);
}

public sealed class VerifiedSynthesisPlan
{
    internal VerifiedSynthesisPlan(DigestEnvelope envelope, IReadOnlyList<SynthesisEligibleRecord> sources)
    {
        Envelope = envelope; Sources = Array.AsReadOnly(sources.ToArray());
        ProtocolVersionId = sources[0].ProtocolVersionId;
        ProtocolContentDigest = sources[0].ProtocolContentDigest;
    }
    public DigestEnvelope Envelope { get; }
    public ContentDigest Digest => Envelope.ComputeDigest();
    public IReadOnlyList<SynthesisEligibleRecord> Sources { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public byte[] ToCanonicalBytes() => Envelope.ToCanonicalJsonBytes();
}

public static class SynthesisPlanAuthority
{
    public static VerifiedSynthesisPlan Create(
        string planId,
        VerifiedProtocolVersion protocol,
        IEnumerable<SynthesisEligibleRecord> eligibleRecords,
        IEnumerable<SynthesisSourceOutcome> sourceOutcomes,
        IEnumerable<SynthesisOutcome> outcomes,
        IEnumerable<string> assumptions,
        IEnumerable<SynthesisTransformation> transformations,
        string missingDataPolicy,
        IEnumerable<string> sensitivityAnalyses,
        IEnumerable<SynthesisCalculationDeclaration> calculations,
        SynthesisActor author,
        DateTimeOffset authoredAt)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(author);
        if (protocol.Version.Status != ProtocolStatus.Approved)
            throw Rule(SynthesisErrorCodes.InvalidAuthority, "Synthesis plans require a current approved Protocol version.");
        if (!author.IsHuman) throw Rule(SynthesisErrorCodes.AutomationCannotAuthorize, "Automation may propose but cannot authorize a synthesis plan.");
        _ = author.ToCanonicalJson();
        var sources = (eligibleRecords ?? throw new ArgumentNullException(nameof(eligibleRecords))).OrderBy(item => item.RecordDigest.ToString(), StringComparer.Ordinal).ToArray();
        if (sources.Length == 0 || sources.Select(item => item.RecordDigest).Distinct().Count() != sources.Length)
            throw Rule(SynthesisErrorCodes.IneligibleSource, "Synthesis plans require unique eligible source records.");
        if (sources.Any(item => !item.IsCurrent || item.IsInvalidated))
            throw Rule(SynthesisErrorCodes.StaleSource, "Synthesis plans reject stale or invalidated source records.");
        if (sources.Any(item => item.ProtocolVersionId != protocol.Version.Id || item.ProtocolContentDigest != protocol.Version.ContentDigest))
            throw Rule(SynthesisErrorCodes.IneligibleSource, "Source records must bind the plan Protocol authority.");

        var normalizedOutcomes = (outcomes ?? throw new ArgumentNullException(nameof(outcomes))).OrderBy(item => item.OutcomeId, StringComparer.Ordinal).ToArray();
        if (normalizedOutcomes.Length == 0 || normalizedOutcomes.Select(item => item.OutcomeId).Distinct(StringComparer.Ordinal).Count() != normalizedOutcomes.Length)
            throw Rule(SynthesisErrorCodes.InvalidAuthority, "Synthesis outcomes must be non-empty and unique.");
        var normalizedMappings = (sourceOutcomes ?? throw new ArgumentNullException(nameof(sourceOutcomes)))
            .OrderBy(item => item.SourceRecordDigest.ToString(), StringComparer.Ordinal).ThenBy(item => item.OutcomeId, StringComparer.Ordinal).ToArray();
        if (normalizedMappings.Length == 0 || normalizedMappings.Any(item => !sources.Any(source => source.RecordDigest == item.SourceRecordDigest)))
            throw Rule(SynthesisErrorCodes.IneligibleSource, "Every source-outcome mapping must resolve to an eligible source.");
        var normalizedTransforms = (transformations ?? Array.Empty<SynthesisTransformation>()).OrderBy(item => item.TransformationId, StringComparer.Ordinal).ToArray();
        foreach (var mapping in normalizedMappings)
        {
            var outcome = normalizedOutcomes.SingleOrDefault(item => item.OutcomeId == mapping.OutcomeId)
                ?? throw Rule(SynthesisErrorCodes.MeasureMismatch, "Source mapping references an unknown outcome.");
            if (mapping.EffectMeasure != outcome.EffectMeasure || mapping.Unit != outcome.Unit)
            {
                var hasTransform = normalizedTransforms.Any(item => item.OutcomeId == mapping.OutcomeId &&
                    item.FromEffectMeasure == mapping.EffectMeasure && item.FromUnit == mapping.Unit &&
                    item.ToEffectMeasure == outcome.EffectMeasure && item.ToUnit == outcome.Unit);
                if (!hasTransform) throw Rule(SynthesisErrorCodes.MeasureMismatch, "Effect measure or unit mismatch requires an explicit transformation.");
            }
        }

        var normalizedAssumptions = RequiredValues(assumptions, "assumptions");
        var normalizedSensitivity = RequiredValues(sensitivityAnalyses, "sensitivity analyses");
        var normalizedCalculations = (calculations ?? throw new ArgumentNullException(nameof(calculations)))
            .OrderBy(item => item.LibraryId, StringComparer.Ordinal).ThenBy(item => item.LibraryVersion, StringComparer.Ordinal).ToArray();
        if (normalizedCalculations.Length == 0)
            throw Rule(SynthesisErrorCodes.InvalidAuthority, "Synthesis plans require a calculation library, version, and configuration declaration.");
        authoredAt = RequireUtc(authoredAt, nameof(authoredAt));
        var content = new CanonicalJsonObject().Add("plan_id", Required(planId)).Add("protocol_version_id", protocol.Version.Id)
            .Add("protocol_content_digest", protocol.Version.ContentDigest.ToString())
            .Add("eligible_records", CanonicalJsonValue.Array(sources.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("source_outcomes", CanonicalJsonValue.Array(normalizedMappings.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("outcomes", CanonicalJsonValue.Array(normalizedOutcomes.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("assumptions", CanonicalJsonValue.Array(normalizedAssumptions.Select(CanonicalJsonValue.From).ToArray()))
            .Add("transformations", CanonicalJsonValue.Array(normalizedTransforms.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("missing_data_policy", Required(missingDataPolicy))
            .Add("sensitivity_analyses", CanonicalJsonValue.Array(normalizedSensitivity.Select(CanonicalJsonValue.From).ToArray()))
            .Add("calculations", CanonicalJsonValue.Array(normalizedCalculations.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("author", author.ToCanonicalJson()).AddTimestamp("authored_at", authoredAt)
            .Add("non_claims", CanonicalJsonValue.Array(new[] { "plan-not-calculation", "no-statistical-conclusion", "no-clinical-or-causal-claim" }.Select(CanonicalJsonValue.From).ToArray()));
        return new VerifiedSynthesisPlan(new DigestEnvelope(DigestScope.CanonicalJsonRecord, SynthesisSchemas.Plan, SynthesisSchemas.Version, content), sources);
    }

    private static string[] RequiredValues(IEnumerable<string> values, string label)
    {
        var result = (values ?? throw new ArgumentNullException(label)).Select(Required).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        return result.Length > 0 ? result : throw Rule(SynthesisErrorCodes.InvalidAuthority, $"Synthesis {label} are required.");
    }
    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw Rule(SynthesisErrorCodes.InvalidAuthority, "Synthesis fields are required.");
    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true)
            ? value
            : throw Rule(SynthesisErrorCodes.InvalidAuthority, $"{name} must be UTC.");
    private static SynthesisRuleException Rule(string category, string message) => new(category, message);
}

public sealed class SynthesisInvalidation
{
    private SynthesisInvalidation(DigestEnvelope envelope, string amendmentId, ContentDigest amendmentDigest, IReadOnlyList<ContentDigest> targets)
    {
        Envelope = envelope; AmendmentId = amendmentId; AmendmentDigest = amendmentDigest; TargetDigests = Array.AsReadOnly(targets.ToArray());
    }
    public DigestEnvelope Envelope { get; }
    public ContentDigest Digest => Envelope.ComputeDigest();
    public string AmendmentId { get; }
    public ContentDigest AmendmentDigest { get; }
    public IReadOnlyList<ContentDigest> TargetDigests { get; }

    public static SynthesisInvalidation Create(
        string invalidationId,
        VerifiedProtocolAmendment amendment,
        SynthesisPlanJournal journal,
        IEnumerable<VerifiedSynthesisPlan> targetPlans,
        string reason,
        SynthesisActor actor,
        DateTimeOffset invalidatedAt)
    {
        ArgumentNullException.ThrowIfNull(amendment); ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(journal);
        if (!actor.IsHuman) throw new SynthesisRuleException(SynthesisErrorCodes.AutomationCannotAuthorize, "Automation cannot invalidate synthesis authority.");
        var plans = (targetPlans ?? throw new ArgumentNullException(nameof(targetPlans))).ToArray();
        if (plans.Length == 0 || plans.Any(plan => !journal.CurrentPlans.Contains(plan)))
            throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Invalidation must target current plans in the supplied synthesis journal.");
        if (plans.Any(plan => amendment.Amendment.AmendsVersionId != plan.ProtocolVersionId ||
            amendment.Amendment.PreviousContentDigest != plan.ProtocolContentDigest))
            throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Invalidation amendment does not match every synthesis plan Protocol authority.");
        var targets = plans.Select(item => item.Digest).Distinct().OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
        if (targets.Length == 0 || targets.Any(item => !item.IsValid)) throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Invalidation requires valid target digests.");
        invalidatedAt = RequireUtc(invalidatedAt, nameof(invalidatedAt));
        var content = new CanonicalJsonObject().Add("invalidation_id", Required(invalidationId))
            .Add("amendment_id", amendment.Amendment.AmendmentId).Add("amendment_digest", amendment.AmendmentDigest.ToString())
            .Add("target_digests", CanonicalJsonValue.Array(targets.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()))
            .Add("reason", Required(reason)).Add("actor", actor.ToCanonicalJson()).AddTimestamp("invalidated_at", invalidatedAt);
        return new SynthesisInvalidation(new DigestEnvelope(DigestScope.CanonicalJsonRecord, SynthesisSchemas.Invalidation, SynthesisSchemas.Version, content), amendment.Amendment.AmendmentId, amendment.AmendmentDigest, targets);
    }
    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, "Invalidation fields are required.");
    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true)
            ? value
            : throw new SynthesisRuleException(SynthesisErrorCodes.InvalidAuthority, $"{name} must be UTC.");
}
