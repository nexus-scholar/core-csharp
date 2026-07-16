using System.Collections.ObjectModel;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Appraisal;

public static class AppraisalSchemas
{
    public const string InstrumentSchemaId = "nexus.appraisal.instrument";
    public const string RecordSchemaId = "nexus.appraisal.record";
    public const string InvalidationSchemaId = "nexus.appraisal.invalidation";
    public const string SchemaVersion = "1.0.0";
    public const string SupportedInstrumentVersion = "1.0.0";
}

public static class AppraisalErrorCodes
{
    public const string MissingInstrumentVersion = "missing-instrument-version";
    public const string UnknownInstrumentVersion = "unknown-instrument-version";
    public const string MissingQuestion = "missing-question-definition";
    public const string DuplicateQuestion = "duplicate-question-definition";
    public const string MissingCandidate = "missing-appraisal-candidate";
    public const string EmptyQuestionText = "empty-question-text";
    public const string DuplicateAnswer = "duplicate-question-answer";
    public const string UnknownQuestion = "unknown-question-answer";
    public const string InvalidAnswer = "invalid-question-answer";
    public const string IncompleteAnswers = "incomplete-appraisal-answers";
    public const string MissingEvidence = "missing-appraisal-evidence";
    public const string InvalidJudgment = "invalid-appraisal-judgment";
    public const string AutomationFinalization = "automation-finalization-restricted";
    public const string MissingActor = "missing-appraisal-actor";
    public const string MissingCorrectionTarget = "missing-correction-target";
    public const string InvalidInvalidation = "invalid-appraisal-invalidation";
}

public sealed class AppraisalRuleException : DomainRuleException
{
    public AppraisalRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public sealed record AppraisalQuestion(string QuestionId, string Prompt)
{
    public string CanonicalQuestionId => Guard.NotBlank(QuestionId, nameof(QuestionId));
    public string CanonicalPrompt => Guard.NotBlank(Prompt, nameof(Prompt));

    public CanonicalJsonObject ToCanonicalJson() =>
        new CanonicalJsonObject()
            .Add("question_id", CanonicalQuestionId)
            .Add("prompt", CanonicalPrompt);
}

public sealed record AppraisalAnswer(string QuestionId, string Answer, IReadOnlyList<FullTextEvidenceLocation>? Evidence = null)
{
    public string CanonicalQuestionId => Guard.NotBlank(QuestionId, nameof(QuestionId));
    public string CanonicalAnswer => Guard.NotBlank(Answer, nameof(Answer));

    public CanonicalJsonObject ToCanonicalJson() =>
        new CanonicalJsonObject()
            .Add("question_id", CanonicalQuestionId)
            .Add("answer", CanonicalAnswer)
            .Add("evidence", CanonicalJsonValue.Array((Evidence ?? Array.Empty<FullTextEvidenceLocation>())
                .OrderBy(item => item.Digest.ToString(), StringComparer.Ordinal).Select(item => item.ToCanonicalJson()).ToArray()));
}

public sealed class AppraisalInstrument
{
    private readonly IReadOnlyList<AppraisalQuestion> _questions;
    private readonly IReadOnlyList<string> _allowedAnswers;
    private readonly IReadOnlyList<string> _judgmentVocabulary;

    private AppraisalInstrument(
        string instrumentId,
        string version,
        string methodDomain,
        IReadOnlyList<AppraisalQuestion> questions,
        IReadOnlyList<string> allowedAnswers,
        IReadOnlyList<string> judgmentVocabulary)
    {
        InstrumentId = Guard.NotBlank(instrumentId, nameof(instrumentId));
        Version = Guard.NotBlank(version, nameof(version));
        MethodDomain = Guard.NotBlank(methodDomain, nameof(methodDomain));
        _questions = Array.AsReadOnly(questions.ToArray());
        _allowedAnswers = Array.AsReadOnly(allowedAnswers.ToArray());
        _judgmentVocabulary = Array.AsReadOnly(judgmentVocabulary.ToArray());

        if (!string.Equals(Version, AppraisalSchemas.SupportedInstrumentVersion, StringComparison.Ordinal))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.UnknownInstrumentVersion, "Unsupported appraisal instrument version.");
        }

        if (_questions.Count == 0)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingQuestion, "Appraisal instruments require at least one question.");
        }

        var normalizedQuestionIds = _questions.Select(item => item.CanonicalQuestionId).ToArray();
        if (normalizedQuestionIds.Distinct(StringComparer.Ordinal).Count() != normalizedQuestionIds.Length)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.DuplicateQuestion, "Question identifiers must be unique.");
        }

        if (_allowedAnswers.Count == 0)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidAnswer, "Allowed answers must contain at least one value.");
        }

        if (_allowedAnswers.Any(answer => string.IsNullOrWhiteSpace(answer)))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidAnswer, "Allowed answers must be non-empty.");
        }

        if (_judgmentVocabulary.Count == 0)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidJudgment, "Judgment vocabulary must contain at least one value.");
        }

        if (_judgmentVocabulary.Any(item => string.IsNullOrWhiteSpace(item)))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidJudgment, "Judgment vocabulary values must be non-empty.");
        }

        DigestEnvelope = BuildEnvelope();
    }

    public string InstrumentId { get; }

    public string Version { get; }

    public string MethodDomain { get; }

    public IReadOnlyList<AppraisalQuestion> Questions => _questions;

    public IReadOnlyList<string> AllowedAnswers => _allowedAnswers;

    public IReadOnlyList<string> JudgmentVocabulary => _judgmentVocabulary;

    public DigestEnvelope DigestEnvelope { get; }

    public ContentDigest Digest => DigestEnvelope.ComputeDigest();

    public static AppraisalInstrument Create(
        string instrumentId,
        string version,
        string methodDomain,
        IEnumerable<AppraisalQuestion> questions,
        IEnumerable<string> allowedAnswers,
        IEnumerable<string> judgmentVocabulary)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingInstrumentVersion, "Appraisal instrument version is required.");
        }

        if (string.IsNullOrWhiteSpace(instrumentId))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingInstrumentVersion, "Appraisal instrument id is required.");
        }

        return new AppraisalInstrument(
            instrumentId,
            version,
            methodDomain,
            (questions ?? throw new ArgumentNullException(nameof(questions))).ToArray(),
            (allowedAnswers ?? throw new ArgumentNullException(nameof(allowedAnswers))).ToArray(),
            (judgmentVocabulary ?? throw new ArgumentNullException(nameof(judgmentVocabulary))).ToArray());
    }

    public CanonicalJsonObject ToCanonicalJson() =>
        new CanonicalJsonObject()
            .Add("instrument_id", InstrumentId)
            .Add("version", Version)
            .Add("method_domain", MethodDomain)
            .Add("questions", CanonicalJsonValue.Array(Questions.Select(question => question.ToCanonicalJson()).ToArray()))
            .Add("allowed_answers", CanonicalJsonValue.Array(AllowedAnswers.Select(CanonicalJsonValue.From).ToArray()))
            .Add("judgment_vocabulary", CanonicalJsonValue.Array(JudgmentVocabulary.Select(CanonicalJsonValue.From).ToArray()));

    public byte[] ToCanonicalBytes() => DigestEnvelope.ToCanonicalJsonBytes();

    public DigestEnvelope BuildEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        AppraisalSchemas.InstrumentSchemaId,
        AppraisalSchemas.SchemaVersion,
        ToCanonicalJson());
}

public sealed class AppraisalRecord
{
    private AppraisalRecord(
        string recordId,
        VerifiedProtocolVersion protocol,
        AppraisalInstrument instrument,
        string candidateId,
        IReadOnlyList<AppraisalAnswer> answers,
        IReadOnlyList<FullTextEvidenceLocation> evidence,
        string overallJudgment,
        string rationale,
        ProtocolActor actor,
        DateTimeOffset appraisedAt,
        bool isProposal,
        bool isCorrection,
        string? sourceProposalDigest,
        string? supersedesRecordId,
        ContentDigest? supersedesRecordDigest)
    {
        RecordId = Guard.NotBlank(recordId, nameof(recordId));
        Protocol = protocol;
        Instrument = instrument;
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        Answers = Array.AsReadOnly((answers ?? Array.Empty<AppraisalAnswer>()).ToArray());
        Evidence = Array.AsReadOnly((evidence ?? Array.Empty<FullTextEvidenceLocation>()).ToArray());
        OverallJudgment = Guard.NotBlank(overallJudgment, nameof(overallJudgment));
        Rationale = Guard.NotBlank(rationale, nameof(rationale));
        Actor = actor;
        AppraisedAt = appraisedAt;
        IsProposal = isProposal;
        IsCorrection = isCorrection;
        SourceProposalDigest = sourceProposalDigest;
        SupersedesRecordId = supersedesRecordId;
        SupersedesRecordDigest = supersedesRecordDigest;
        DigestEnvelope = BuildEnvelope();
    }

    public string RecordId { get; }
    public VerifiedProtocolVersion Protocol { get; }
    public AppraisalInstrument Instrument { get; }
    public string CandidateId { get; }
    public ReadOnlyCollection<AppraisalAnswer> Answers { get; }
    public ReadOnlyCollection<FullTextEvidenceLocation> Evidence { get; }
    public string OverallJudgment { get; }
    public string Rationale { get; }
    public ProtocolActor Actor { get; }
    public DateTimeOffset AppraisedAt { get; }
    public bool IsProposal { get; }
    public bool IsCorrection { get; }
    public string? SourceProposalDigest { get; }
    public string? SupersedesRecordId { get; }
    public ContentDigest? SupersedesRecordDigest { get; }
    public DigestEnvelope DigestEnvelope { get; }
    public ContentDigest Digest => DigestEnvelope.ComputeDigest();

    public string ProtocolVersionDigest => Protocol.Version.ContentDigest.ToString();
    public string ProtocolVersionId => Protocol.Version.Id;

    public static AppraisalRecord CreateProposal(
        string recordId,
        VerifiedProtocolVersion protocol,
        AppraisalInstrument instrument,
        string candidateId,
        IEnumerable<AppraisalAnswer> answers,
        string overallJudgment,
        string rationale,
        ProtocolActor actor,
        IClock clock,
        IEnumerable<FullTextEvidenceLocation>? evidence = null)
    {
        return Create(recordId, protocol, instrument, candidateId, answers, evidence, overallJudgment, rationale, actor, clock, true, false, null, null, null);
    }

    public static AppraisalRecord CreateFinal(
        string recordId,
        VerifiedProtocolVersion protocol,
        AppraisalInstrument instrument,
        string candidateId,
        IEnumerable<AppraisalAnswer> answers,
        string overallJudgment,
        string rationale,
        ProtocolActor actor,
        IClock clock,
        IEnumerable<FullTextEvidenceLocation>? evidence)
    {
        return Create(recordId, protocol, instrument, candidateId, answers, evidence, overallJudgment, rationale, actor, clock, false, false, null, null, null);
    }

    public static AppraisalRecord CreateCorrection(
        string recordId,
        VerifiedProtocolVersion protocol,
        AppraisalInstrument instrument,
        string candidateId,
        IEnumerable<AppraisalAnswer> answers,
        string overallJudgment,
        string rationale,
        ProtocolActor actor,
        IClock clock,
        IEnumerable<FullTextEvidenceLocation>? evidence,
        string supersedesRecordId,
        ContentDigest supersedesRecordDigest)
    {
        return Create(
            recordId,
            protocol,
            instrument,
            candidateId,
            answers,
            evidence,
            overallJudgment,
            rationale,
            actor,
            clock,
            false,
            true,
            null,
            supersedesRecordId,
            supersedesRecordDigest);
    }

    private static AppraisalRecord Create(
        string recordId,
        VerifiedProtocolVersion protocol,
        AppraisalInstrument instrument,
        string candidateId,
        IEnumerable<AppraisalAnswer> answers,
        IEnumerable<FullTextEvidenceLocation>? evidence,
        string overallJudgment,
        string rationale,
        ProtocolActor actor,
        IClock clock,
        bool isProposal,
        bool isCorrection,
        string? sourceProposalDigest,
        string? supersedesRecordId,
        ContentDigest? supersedesRecordDigest)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(clock);
        if (protocol.Version.Status != ProtocolStatus.Approved)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidInvalidation, "Appraisal records require a current approved Protocol version.");
        }
        if (actor.Id.Value.Length == 0)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingActor, "Appraisal actor is required.");
        }

        var normalizedAnswers = ValidateAndNormalizeAnswers(instrument, answers);
        if (isProposal && normalizedAnswers.Count == 0)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.IncompleteAnswers, "Appraisal answers are required.");
        }

        if (!isProposal && normalizedAnswers.Count != instrument.Questions.Count)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.IncompleteAnswers, "A final appraisal must answer all questions.");
        }

        if (!isProposal && (!instrument.JudgmentVocabulary.Contains(overallJudgment, StringComparer.Ordinal)))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidJudgment, "Final appraisal judgment must match the instrument vocabulary.");
        }

        if (!isProposal && !actor.IsHuman)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.AutomationFinalization, "Automation actors can only create appraisal proposals.");
        }

        if (!isProposal && (evidence is null || evidence.Any() == false))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingEvidence, "Final appraisal requires at least one evidence location.");
        }
        if (!isProposal && normalizedAnswers.Any(answer => answer.Evidence is null || answer.Evidence.Count == 0))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingEvidence, "Every final appraisal answer requires exact Full Text evidence.");
        }

        if (isCorrection)
        {
            if (string.IsNullOrWhiteSpace(supersedesRecordId))
            {
                throw new AppraisalRuleException(AppraisalErrorCodes.MissingCorrectionTarget, "Correction records must supersede exactly one prior record.");
            }
            if (!supersedesRecordDigest.HasValue || !supersedesRecordDigest.Value.IsValid)
            {
                throw new AppraisalRuleException(AppraisalErrorCodes.MissingCorrectionTarget, "Correction records must bind a valid superseded record digest.");
            }
        }

        return new AppraisalRecord(
            recordId,
            protocol,
            instrument,
            candidateId,
            normalizedAnswers,
            (evidence ?? Array.Empty<FullTextEvidenceLocation>()).ToArray(),
            overallJudgment,
            rationale,
            actor,
            RequireUtc(clock.UtcNow, nameof(clock.UtcNow)),
            isProposal,
            isCorrection,
            sourceProposalDigest,
            supersedesRecordId,
            supersedesRecordDigest);
    }

    public CanonicalJsonObject ToCanonicalJson() => DigestEnvelope.ToCanonicalJsonObject();

    public byte[] ToCanonicalBytes() => DigestEnvelope.ToCanonicalJsonBytes();

    private DigestEnvelope BuildEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        AppraisalSchemas.RecordSchemaId,
        AppraisalSchemas.SchemaVersion,
        BuildCanonical());

    private CanonicalJsonObject BuildCanonical()
    {
        var content = new CanonicalJsonObject()
            .Add("record_id", RecordId)
            .Add("protocol_id", Protocol.Version.ProtocolId)
            .Add("protocol_version_id", ProtocolVersionId)
            .Add("protocol_version_digest", ProtocolVersionDigest)
            .Add("instrument_id", Instrument.InstrumentId)
            .Add("instrument_version", Instrument.Version)
            .Add("instrument_digest", Instrument.Digest.ToString())
            .Add("candidate_id", CandidateId)
            .Add("answers", CanonicalJsonValue.Array(Answers.Select(answer => answer.ToCanonicalJson()).ToArray()))
            .Add("overall_judgment", OverallJudgment)
            .Add("rationale", Rationale)
            .Add("appraised_by", Actor.Id.ToString())
            .AddTimestamp("appraised_at", AppraisedAt)
            .Add("is_human", Actor.IsHuman)
            .Add("is_proposal", IsProposal)
            .Add("is_correction", IsCorrection)
            .Add("evidence", CanonicalJsonValue.Array(Evidence.Select(item => item.ToCanonicalJson()).ToArray()));

        if (SourceProposalDigest is not null)
        {
            content.Add("source_proposal_digest", SourceProposalDigest);
        }

        if (SupersedesRecordId is not null)
        {
            content.Add("supersedes_record_id", SupersedesRecordId);
        }

        if (SupersedesRecordDigest.HasValue)
        {
            content.Add("supersedes_record_digest", SupersedesRecordDigest.Value.ToString());
        }

        return content;
    }

    private static List<AppraisalAnswer> ValidateAndNormalizeAnswers(AppraisalInstrument instrument, IEnumerable<AppraisalAnswer>? answers)
    {
        if (answers is null)
        {
            return new List<AppraisalAnswer>();
        }

        var provided = answers.Select(answer => answer.CanonicalQuestionId)
            .ToArray();
        if (provided.Length != provided.Distinct(StringComparer.Ordinal).Count())
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.DuplicateAnswer, "Each question can only be answered once.");
        }

        var lookup = answers
            .Select(answer => new AppraisalAnswer(answer.CanonicalQuestionId, answer.CanonicalAnswer,
                Array.AsReadOnly((answer.Evidence ?? Array.Empty<FullTextEvidenceLocation>()).ToArray())))
            .ToDictionary(answer => answer.CanonicalQuestionId, StringComparer.Ordinal);

        var normalized = new List<AppraisalAnswer>();
        foreach (var question in instrument.Questions)
        {
            if (!lookup.TryGetValue(question.QuestionId, out var answer))
            {
                continue;
            }

            if (!instrument.AllowedAnswers.Contains(answer.Answer, StringComparer.Ordinal))
            {
                throw new AppraisalRuleException(AppraisalErrorCodes.InvalidAnswer, $"Answer for '{question.QuestionId}' is invalid.");
            }
            normalized.Add(answer);
        }

        if (lookup.Keys.Any(key => instrument.Questions.All(question => key != question.QuestionId)))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.UnknownQuestion, "Answers must target questions declared in the instrument.");
        }

        return normalized;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        value.Offset == TimeSpan.Zero ? value : throw new AppraisalRuleException(AppraisalErrorCodes.InvalidInvalidation, $"{name} must be UTC.");
}

public sealed class AppraisalAmendmentInvalidation
{
    private AppraisalAmendmentInvalidation(
        string invalidationId,
        VerifiedProtocolAmendment amendment,
        IReadOnlyList<ContentDigest> affectedAppraisalDigests,
        string rationale,
        ProtocolActor actor,
        DateTimeOffset invalidatedAt)
    {
        InvalidationId = Guard.NotBlank(invalidationId, nameof(invalidationId));
        Amendment = amendment ?? throw new ArgumentNullException(nameof(amendment));
        AffectedAppraisalDigests = Array.AsReadOnly((affectedAppraisalDigests ?? throw new ArgumentNullException(nameof(affectedAppraisalDigests))).ToArray());
        Rationale = Guard.NotBlank(rationale, nameof(rationale));
        Actor = actor;
        InvalidatedAt = RequireUtc(invalidatedAt, nameof(invalidatedAt));

        if (!actor.IsHuman)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingActor, "Amendment invalidation requires a human actor.");
        }

        var distinct = AffectedAppraisalDigests
            .Where(item => item.IsValid)
            .Select(item => item)
            .Distinct()
            .ToArray();
        if (distinct.Length == 0 || distinct.Length != AffectedAppraisalDigests.Count)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidInvalidation, "Amendment invalidation requires distinct valid affected appraisal digests.");
        }
    }

    public string InvalidationId { get; }
    public VerifiedProtocolAmendment Amendment { get; }
    public ReadOnlyCollection<ContentDigest> AffectedAppraisalDigests { get; }
    public string Rationale { get; }
    public ProtocolActor Actor { get; }
    public DateTimeOffset InvalidatedAt { get; }

    public static AppraisalAmendmentInvalidation Create(
        string invalidationId,
        VerifiedProtocolAmendment amendment,
        AppraisalJournal journal,
        IEnumerable<AppraisalRecord> affectedAppraisals,
        string rationale,
        ProtocolActor actor,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(journal);
        var targets = (affectedAppraisals ?? throw new ArgumentNullException(nameof(affectedAppraisals))).ToArray();
        if (targets.Length == 0 || targets.Any(item => !journal.CurrentRecords.Contains(item)))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidInvalidation, "Invalidation must target current records in the supplied appraisal journal.");
        }
        if (targets.Any(item => amendment.Amendment.AmendsVersionId != item.ProtocolVersionId ||
            amendment.Amendment.PreviousContentDigest != item.Protocol.Version.ContentDigest))
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.InvalidInvalidation, "Invalidation amendment does not match every appraisal Protocol authority.");
        }

        if (actor.Id.Value.Length == 0)
        {
            throw new AppraisalRuleException(AppraisalErrorCodes.MissingActor, "Amendment invalidation requires an actor.");
        }

        return new AppraisalAmendmentInvalidation(
            invalidationId,
            amendment,
            targets.Select(item => item.Digest).ToArray(),
            rationale,
            actor,
            clock.UtcNow);
    }

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("invalidation_id", InvalidationId)
            .Add("amendment_id", Amendment.Amendment.AmendmentId)
            .Add("amendment_digest", Amendment.AmendmentDigest.ToString())
            .Add("rationale", Rationale)
            .Add("invalidated_by", Actor.Id.ToString())
            .AddTimestamp("invalidated_at", InvalidatedAt)
            .Add("affected_appraisal_digests", CanonicalJsonValue.Array(AffectedAppraisalDigests.Select(digest => CanonicalJsonValue.From(digest.ToString())).ToArray()));
    }

    public byte[] ToCanonicalBytes() => ToEnvelope().ToCanonicalJsonBytes();

    public ContentDigest Digest => ToEnvelope().ComputeDigest();

    private DigestEnvelope ToEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        AppraisalSchemas.InvalidationSchemaId,
        AppraisalSchemas.SchemaVersion,
        ToCanonicalJson());

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        value.Offset == TimeSpan.Zero ? value : throw new AppraisalRuleException(AppraisalErrorCodes.InvalidInvalidation, $"{name} must be UTC.");
}
