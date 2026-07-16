using NexusScholar.Appraisal;
using NexusScholar.Extraction;

namespace NexusScholar.Synthesis;

public sealed partial class SynthesisEligibleRecord
{
    public static SynthesisEligibleRecord FromExtraction(
        ExtractionRecord record,
        ExtractionJournal journal,
        IEnumerable<ExtractionAmendmentInvalidation>? invalidations = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(journal);
        if (!journal.Records.Any(item => item.Digest == record.Digest) || record.Kind == ExtractionRecordKind.Proposal)
            throw new SynthesisRuleException(SynthesisErrorCodes.IneligibleSource, "Extraction source must be a final record in the supplied journal.");
        var invalidated = (invalidations ?? Array.Empty<ExtractionAmendmentInvalidation>())
            .Any(item => item.AffectedRecordDigests.Contains(record.Digest));
        return new SynthesisEligibleRecord(
            "extraction", record.RecordId, record.Digest, record.FormCandidateId,
            journal.Form.ProtocolVersionId, journal.Form.ProtocolContentDigest,
            journal.CurrentRecords(record.FormCandidateId).Any(item => item.Digest == record.Digest), invalidated);
    }

    public static SynthesisEligibleRecord FromAppraisal(
        AppraisalRecord record,
        AppraisalJournal journal,
        IEnumerable<AppraisalAmendmentInvalidation>? invalidations = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(journal);
        if (!journal.Records.Any(item => item.Digest == record.Digest) || record.IsProposal)
            throw new SynthesisRuleException(SynthesisErrorCodes.IneligibleSource, "Appraisal source must be a final record in the supplied history.");
        var invalidated = (invalidations ?? Array.Empty<AppraisalAmendmentInvalidation>())
            .Any(item => item.AffectedAppraisalDigests.Contains(record.Digest));
        return new SynthesisEligibleRecord(
            "appraisal", record.RecordId, record.Digest, record.CandidateId,
            record.ProtocolVersionId, record.Protocol.Version.ContentDigest,
            journal.CurrentRecords.Any(item => item.Digest == record.Digest), invalidated);
    }
}
