namespace NexusScholar.Appraisal;

public sealed class AppraisalJournal
{
    private readonly List<AppraisalRecord> _records = [];
    private readonly List<AppraisalAmendmentInvalidation> _invalidations = [];

    public IReadOnlyList<AppraisalRecord> Records => _records.AsReadOnly();
    public IReadOnlyList<AppraisalAmendmentInvalidation> Invalidations => _invalidations.AsReadOnly();
    public IReadOnlyList<AppraisalRecord> CurrentRecords
    {
        get
        {
            var superseded = _records.Where(item => item.SupersedesRecordDigest.HasValue)
                .Select(item => item.SupersedesRecordDigest!.Value).ToHashSet();
            var invalidated = _invalidations.SelectMany(item => item.AffectedAppraisalDigests).ToHashSet();
            return _records.Where(item => !item.IsProposal && !superseded.Contains(item.Digest) && !invalidated.Contains(item.Digest)).ToArray();
        }
    }

    public void Append(AppraisalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (_records.Any(item => item.RecordId == record.RecordId || item.Digest == record.Digest))
            throw Rule("Appraisal record id and digest must be unique in a journal.");
        if (record.IsCorrection)
        {
            var target = CurrentRecords.SingleOrDefault(item => item.Digest == record.SupersedesRecordDigest && item.RecordId == record.SupersedesRecordId);
            if (target is null || target.CandidateId != record.CandidateId || target.ProtocolVersionId != record.ProtocolVersionId || target.Instrument.Digest != record.Instrument.Digest)
                throw Rule("Appraisal correction must supersede one matching current record.");
        }
        _records.Add(record);
    }

    public void Append(AppraisalAmendmentInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        if (_invalidations.Any(item => item.InvalidationId == invalidation.InvalidationId || item.Digest == invalidation.Digest))
            throw Rule("Appraisal invalidation id and digest must be unique.");
        if (invalidation.AffectedAppraisalDigests.Any(digest => CurrentRecords.All(item => item.Digest != digest)))
            throw Rule("Appraisal invalidation must target only current records.");
        _invalidations.Add(invalidation);
    }

    private static AppraisalRuleException Rule(string message) => new(AppraisalErrorCodes.InvalidInvalidation, message);
}
