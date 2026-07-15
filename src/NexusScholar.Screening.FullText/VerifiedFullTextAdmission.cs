using System.Text.Json;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Screening;

namespace NexusScholar.Screening.FullText;

public static class VerifiedFullTextAdmissionSchema
{
    public const string SchemaId = "nexus.fulltext.admission";
    public const string SchemaVersion = "1.0.0";
}

public sealed class VerifiedFullTextAdmission
{
    private readonly FullTextInput _input;

    private VerifiedFullTextAdmission(
        string conductId,
        ContentDigest policyDigest,
        string handoffId,
        ContentDigest handoffDigest,
        string candidateSetId,
        string candidateId,
        string verdict,
        string screeningDecisionId,
        IReadOnlyList<ContentDigest> supportingDecisionDigests,
        FullTextInput input,
        ContentDigest inputDigest)
    {
        ConductId = Guard.NotBlank(conductId, nameof(conductId));
        PolicyDigest = policyDigest;
        HandoffId = Guard.NotBlank(handoffId, nameof(handoffId));
        HandoffDigest = handoffDigest;
        CandidateSetId = Guard.NotBlank(candidateSetId, nameof(candidateSetId));
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        Verdict = Guard.NotBlank(verdict, nameof(verdict));
        ScreeningDecisionId = Guard.NotBlank(screeningDecisionId, nameof(screeningDecisionId));
        SupportingDecisionDigests = Array.AsReadOnly(supportingDecisionDigests.ToArray());
        _input = input;
        InputDigest = inputDigest;
        Digest = Envelope().ComputeDigest();
    }

    public string ConductId { get; }
    public ContentDigest PolicyDigest { get; }
    public string HandoffId { get; }
    public ContentDigest HandoffDigest { get; }
    public string CandidateSetId { get; }
    public string CandidateId { get; }
    public string Verdict { get; }
    public string ScreeningDecisionId { get; }
    public IReadOnlyList<ContentDigest> SupportingDecisionDigests { get; }
    public FullTextInput Input => _input;
    public ContentDigest InputDigest { get; }
    public ContentDigest Digest { get; }

    public static VerifiedFullTextAdmission Create(
        ScreeningConductJournal journal,
        ScreeningConductHandoff handoff,
        string candidateId)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(handoff);
        var targetCandidateId = Guard.NotBlank(candidateId, nameof(candidateId));

        if (!string.Equals(handoff.ConductId, journal.Header.ConductId, StringComparison.Ordinal) ||
            handoff.PolicyDigest != journal.Policy.Digest)
        {
            throw Rule(FullTextErrorCodes.InvalidAuthorityChain, "Screening conduct and handoff authority are mismatched.");
        }

        if (handoff.JournalHeadDigest != journal.Projection.HeadDigest)
        {
            throw Rule(FullTextErrorCodes.InvalidAuthorityChain, "Screening handoff is not current to the supplied journal.");
        }

        if (!journal.Projection.Outcomes.TryGetValue(targetCandidateId, out var outcome))
        {
            throw Rule(FullTextErrorCodes.MissingCandidateBinding, "Screening handoff does not contain an outcome for this candidate.");
        }

        if (!string.Equals(outcome.Verdict, ScreeningVerdicts.Include, StringComparison.Ordinal))
        {
            throw Rule(FullTextErrorCodes.InvalidAuthorityChain, "FE-05 admission requires an include Screening outcome.");
        }

        var currentDecisionDigests = CurrentCandidateDecisionDigests(journal, targetCandidateId);
        if (currentDecisionDigests.Count == 0)
        {
            throw Rule(FullTextErrorCodes.MissingCandidateBinding, "Candidate has no current Screening decision set.");
        }

        if (!currentDecisionDigests.SequenceEqual(outcome.SupportingDecisionDigests))
        {
            throw Rule(FullTextErrorCodes.InvalidAuthorityChain, "Screening support digests are incomplete or tampered.");
        }

        var decidingDecision = journal.Decisions
            .Where(item => item.CandidateId == targetCandidateId && currentDecisionDigests.Contains(item.Digest))
            .OrderByDescending(item => item.Ordinal)
            .First();

        var input = BuildInput(
            journal.Header.ConductId,
            journal.Policy.Digest,
            handoff.HandoffId,
            handoff.Digest,
            journal.Header.CandidateSetId,
            targetCandidateId,
            outcome.Verdict,
            decidingDecision.DecisionId,
            currentDecisionDigests);

        var inputDigest = ContentDigest.Sha256(FullTextAuthorityCanonicalCodec.Serialize(input));

        return new VerifiedFullTextAdmission(
            journal.Header.ConductId,
            journal.Policy.Digest,
            handoff.HandoffId,
            handoff.Digest,
            journal.Header.CandidateSetId,
            targetCandidateId,
            outcome.Verdict,
            decidingDecision.DecisionId,
            currentDecisionDigests,
            input,
            inputDigest);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    internal DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        VerifiedFullTextAdmissionSchema.SchemaId,
        VerifiedFullTextAdmissionSchema.SchemaVersion,
        new CanonicalJsonObject()
            .Add("conduct_id", ConductId)
            .Add("policy_digest", PolicyDigest.ToString())
            .Add("handoff_id", HandoffId)
            .Add("handoff_digest", HandoffDigest.ToString())
            .Add("candidate_set_id", CandidateSetId)
            .Add("candidate_id", CandidateId)
            .Add("verdict", Verdict)
            .Add("screening_decision_id", ScreeningDecisionId)
            .Add("supporting_decision_digests", CanonicalJsonValue.Array(SupportingDecisionDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()))
            .Add("input_id", Input.InputId)
            .Add("input_digest", InputDigest.ToString()));

    private static List<ContentDigest> CurrentCandidateDecisionDigests(ScreeningConductJournal journal, string candidateId)
    {
        var invalidated = journal.Projection.InvalidatedDecisionDigests;
        var superseded = journal.Decisions
            .Where(item => item.CandidateId == candidateId && item.Kind == ScreeningConductDecisionKind.Correction && item.SupersedesDecisionDigest is not null)
            .Select(item => item.SupersedesDecisionDigest!.Value)
            .ToHashSet();
        return journal.Decisions
            .Where(item => item.CandidateId == candidateId && !invalidated.Contains(item.Digest) && !superseded.Contains(item.Digest))
            .Select(item => item.Digest)
            .OrderBy(item => item.ToString(), StringComparer.Ordinal)
            .ToList();
    }

    private static FullTextInput BuildInput(
        string conductId,
        ContentDigest policyDigest,
        string handoffId,
        ContentDigest handoffDigest,
        string candidateSetId,
        string candidateId,
        string verdict,
        string screeningDecisionId,
        IReadOnlyList<ContentDigest> supportingDecisionDigests)
    {
        var seed = new CanonicalJsonObject()
            .Add("conduct_id", conductId)
            .Add("policy_digest", policyDigest.ToString())
            .Add("handoff_id", handoffId)
            .Add("handoff_digest", handoffDigest.ToString())
            .Add("candidate_set_id", candidateSetId)
            .Add("candidate_id", candidateId)
            .Add("verdict", verdict)
            .Add("screening_decision_id", screeningDecisionId)
            .Add("supporting_decision_digests", CanonicalJsonValue.Array(supportingDecisionDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()));
        var inputId = "fulltext-admission-" + ContentDigest.Sha256CanonicalJson(seed).Value[7..19];

        return FullTextInput.FromScreeningDecision(
            inputId,
            candidateSetId,
            candidateId,
            screeningDecisionId,
            ScreeningStages.TitleAbstract,
            FullTextScreeningVerdicts.Include,
            sourceRefs: [new FullTextSourceRef(FullTextSourceKinds.ScreeningHandoff, handoffId)]);
    }

    private static FullTextRuleException Rule(string category, string message) => new(category, message);
}

public static class VerifiedFullTextAdmissionCanonicalCodec
{
    public static byte[] Serialize(VerifiedFullTextAdmission admission) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes((admission ?? throw new ArgumentNullException(nameof(admission))).ToCanonicalJson());

    public static VerifiedFullTextAdmission Rehydrate(
        byte[] bytes,
        ContentDigest expectedDigest,
        ScreeningConductJournal journal,
        ScreeningConductHandoff handoff)
    {
        var content = ParseEnvelope(bytes, expectedDigest, VerifiedFullTextAdmissionSchema.SchemaId, VerifiedFullTextAdmissionSchema.SchemaVersion);
        RequireExact(content,
            [
                "conduct_id", "candidate_id", "candidate_set_id", "handoff_digest", "handoff_id", "input_digest", "input_id",
                "policy_digest", "screening_decision_id", "supporting_decision_digests", "verdict"
            ]);

        var admission = VerifiedFullTextAdmission.Create(
            journal,
            handoff,
            Text(content, "candidate_id"));

        if (admission.ConductId != Text(content, "conduct_id") ||
            admission.PolicyDigest != Digest(content, "policy_digest") ||
            admission.HandoffId != Text(content, "handoff_id") ||
            admission.HandoffDigest != Digest(content, "handoff_digest") ||
            admission.CandidateSetId != Text(content, "candidate_set_id") ||
            admission.CandidateId != Text(content, "candidate_id") ||
            admission.Verdict != Text(content, "verdict") ||
            admission.ScreeningDecisionId != Text(content, "screening_decision_id") ||
            admission.Input.InputId != Text(content, "input_id") ||
            admission.InputDigest != Digest(content, "input_digest") ||
            !admission.SupportingDecisionDigests.SequenceEqual(Array(content, "supporting_decision_digests").Select(item => item is CanonicalJsonString text
                ? ContentDigest.Parse(text.Value)
                : throw Rule("Supporting decision digest must be text."))))
        {
            throw Rule("Screening admission canonical record does not match reconstructed authority.");
        }

        var reproduced = Serialize(admission);
        if (!reproduced.SequenceEqual(bytes))
        {
            throw Rule("Screening admission canonical bytes are not reproducible.");
        }

        return admission;
    }

    private static CanonicalJsonObject ParseEnvelope(byte[] bytes, ContentDigest expectedDigest, string schemaId, string schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        if (CanonicalJsonValue.FromJsonElement(document.RootElement) is not CanonicalJsonObject root ||
            !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
        {
            throw Rule("Full Text admission canonical bytes must be canonical JSON.");
        }

        return DigestEnvelope.RehydrateAndVerify(
            document.RootElement,
            expectedDigest,
            DigestScope.CanonicalJsonRecord,
            schemaId,
            schemaVersion).Envelope.Content;
    }

    private static void RequireExact(CanonicalJsonObject value, IEnumerable<string> required, IEnumerable<string>? optional = null)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        var allowed = requiredSet.Concat(optional ?? System.Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        if (!requiredSet.IsSubsetOf(value.Properties.Keys) || value.Properties.Keys.Any(key => !allowed.Contains(key)))
        {
            throw Rule("Screening admission canonical record has missing or unknown fields.");
        }
    }

    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name)
    {
        if (root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonArray array)
        {
            return array.Items;
        }

        throw Rule($"Screening admission canonical field '{name}' must be an array.");
    }

    private static string Text(CanonicalJsonObject root, string name)
    {
        if (root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonString text)
        {
            return text.Value;
        }

        throw Rule($"Screening admission canonical field '{name}' must be text.");
    }

    private static ContentDigest Digest(CanonicalJsonObject root, string name)
    {
        if (root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonString text)
        {
            try
            {
                return ContentDigest.Parse(text.Value);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                throw Rule($"Screening admission digest '{name}' is invalid: {exception.Message}");
            }
        }

        throw Rule($"Screening admission canonical field '{name}' must be a digest text.");
    }

    private static FullTextRuleException Rule(string message) => new(FullTextErrorCodes.InvalidAuthorityChain, message);
}
