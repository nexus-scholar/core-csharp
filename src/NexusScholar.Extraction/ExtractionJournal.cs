namespace NexusScholar.Extraction;

public sealed class ExtractionJournal
{
    private readonly List<IExtractionJournalEntry> _entries = [];
    private readonly List<ExtractionRecord> _records = [];
    private readonly List<ExtractionAmendmentInvalidation> _invalidations = [];
    private readonly Dictionary<string, ExtractionFieldDefinition> _formFields;

    private ExtractionJournal(ExtractionForm form)
    {
        Form = form;
        _formFields = form.Fields.ToDictionary(item => item.CanonicalFieldId, StringComparer.Ordinal);
        Projection = RecomputeProjection();
    }

    public ExtractionForm Form { get; }
    public IReadOnlyList<IExtractionJournalEntry> Entries => _entries.AsReadOnly();
    public IReadOnlyList<ExtractionRecord> Records => _records.AsReadOnly();
    public IReadOnlyList<ExtractionAmendmentInvalidation> Invalidations => _invalidations.AsReadOnly();
    public ExtractionProjection Projection { get; private set; }

    public static ExtractionJournal Create(ExtractionForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return new ExtractionJournal(form);
    }

    public void Append(ExtractionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (_records.Any(item => item.RecordId == record.RecordId))
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "Extraction record id must be unique in a journal.");
        ValidateRecordBinding(record);

        var currentBefore = CurrentRecordSet();
        var nextOrdinal = _entries.Count + 1;
        var previousDigest = Projection.HeadDigest;

        record.AttachChain(
            nextOrdinal,
            previousDigest,
            Form.FormId,
            Form.Digest,
            _formFields,
            requireFinalValues: record.Kind is not ExtractionRecordKind.Proposal and not ExtractionRecordKind.Resolution);

        ValidateRecordConsistency(record, currentBefore);

        _records.Add(record);
        _entries.Add(record);
        Projection = RecomputeProjection();
    }

    public void Append(ExtractionAmendmentInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        if (_invalidations.Any(item => item.InvalidationId == invalidation.InvalidationId))
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "Extraction invalidation id must be unique in a journal.");
        ValidateInvalidationBinding(invalidation);

        var currentBefore = CurrentRecordSet();
        invalidation.AttachChain(_entries.Count + 1, Projection.HeadDigest, Form.FormId, Form.Digest);
        ValidateInvalidationScope(invalidation, currentBefore);

        _invalidations.Add(invalidation);
        _entries.Add(invalidation);
        Projection = RecomputeProjection();
    }

    public IReadOnlyList<ExtractionRecord> CurrentRecords(string candidateId)
    {
        return Projection.CurrentRecordDigestsByCandidate.TryGetValue(Guard.NotBlank(candidateId, nameof(candidateId)), out var current)
            ? current.Select(FindRecord).Where(item => item is not null).Select(item => item!).ToArray()
            : [];
    }

    public IReadOnlyList<ExtractionConflict> CurrentConflicts(string candidateId)
    {
        return Projection.DisagreementsByCandidate.TryGetValue(Guard.NotBlank(candidateId, nameof(candidateId)), out var conflicts)
            ? conflicts
            : [];
    }

    private void ValidateRecordBinding(ExtractionRecord record)
    {
        if (!string.Equals(record.FormId, Form.FormId, StringComparison.Ordinal) || record.FormDigest != Form.Digest)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Record does not bind the current extraction form.");
        }
    }

    private void ValidateRecordConsistency(ExtractionRecord record, Dictionary<string, IReadOnlyList<ContentDigest>> currentBefore)
    {
        if (!string.Equals(record.FormCandidateId, Form.CandidateId, StringComparison.Ordinal))
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Record candidate must match the form candidate.");
        }

        if (record.Kind is not ExtractionRecordKind.Proposal && !record.Actor.IsHuman)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.AutomationCannotFinalize, "Automation can only create proposals.");
        }

        if (record.Kind == ExtractionRecordKind.Correction && record.SourceRecordDigest is null)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Correction requires a source record.");
        }

        var currentForCandidate = currentBefore.TryGetValue(Form.CandidateId, out var candidateRecords)
            ? candidateRecords.ToHashSet()
            : [];

        if (record.Kind == ExtractionRecordKind.Correction)
        {
            if (record.SourceRecordDigest is null || !record.SourceRecordDigest.Value.IsValid)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Correction source must be a valid digest.");
            }

            if (!currentForCandidate.Contains(record.SourceRecordDigest.Value))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Correction must target a current record.");
            }
        }

        if (record.Kind == ExtractionRecordKind.Resolution)
        {
            if (string.IsNullOrWhiteSpace(record.SourceConflictId))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.RecordConflictResolutionNotFound, "Resolution requires a conflict id.");
            }

            if (record.SourceRecordDigests.Count < 2)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.ResolutionTargetInvalid, "Resolution must target two or more source records.");
            }

            var sourceSetOrdered = record.SourceRecordDigests
                .OrderBy(digest => digest.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (!sourceSetOrdered.All(digest => digest.IsValid) || !sourceSetOrdered.All(digest => currentForCandidate.Contains(digest)))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.ResolutionTargetInvalid, "Resolution source records must be current and valid.");
            }

            if (record.Values.Count != 1)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldValue, "Resolution records must contain one disputed field value.");
            }

            var candidateConflicts = BuildConflicts(currentBefore)
                .TryGetValue(Form.CandidateId, out var conflicts) ? conflicts : [];
            var targetField = record.Values[0].FieldId;
            var unresolved = candidateConflicts.FirstOrDefault(conflict =>
                string.Equals(conflict.ConflictId, record.SourceConflictId, StringComparison.Ordinal) &&
                string.Equals(conflict.FieldId, targetField, StringComparison.Ordinal) &&
                !conflict.Resolved &&
                SameDigestSet(conflict.SourceRecordDigests, sourceSetOrdered));
            if (unresolved is null)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.RecordConflictResolutionNotFound, "Resolution must bind an unresolved disagreement.");
            }
        }
    }

    private void ValidateInvalidationBinding(ExtractionAmendmentInvalidation invalidation)
    {
        if (!string.Equals(invalidation.FormId, Form.FormId, StringComparison.Ordinal) || invalidation.FormDigest != Form.Digest)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Invalidation does not bind the current extraction form.");
        }

        if (!invalidation.Actor.IsHuman)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidActor, "Amendment invalidation requires a human actor.");
        }
    }

    private void ValidateInvalidationScope(
        ExtractionAmendmentInvalidation invalidation,
        Dictionary<string, IReadOnlyList<ContentDigest>> currentBefore)
    {
        var currentDigests = currentBefore.Values.SelectMany(item => item).ToHashSet();
        if (invalidation.AffectedRecordDigests.Any(target => !currentDigests.Contains(target)))
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidationScopeInvalid, "Invalidation must target only current records.");
        }
    }

    private Dictionary<string, IReadOnlyList<ContentDigest>> CurrentRecordSet()
    {
        var invalidated = GetInvalidatedRecordDigests();
        var superseded = new HashSet<ContentDigest>();

        foreach (var record in _records)
        {
            if (record.Kind == ExtractionRecordKind.Correction && record.SourceRecordDigest.HasValue)
            {
                superseded.Add(record.SourceRecordDigest.Value);
            }
            else if (record.Kind == ExtractionRecordKind.Resolution)
            {
                foreach (var source in record.SourceRecordDigests)
                {
                    superseded.Add(source);
                }
            }
        }

        var current = _records
            .Where(record => record.Kind != ExtractionRecordKind.Proposal &&
                !superseded.Contains(record.Digest) && !invalidated.Contains(record.Digest))
            .ToArray();

        return current
            .GroupBy(item => item.FormCandidateId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentDigest>)group.Select(item => item.Digest)
                    .OrderBy(item => item.ToString(), StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private Dictionary<string, IReadOnlyList<ExtractionConflict>> BuildConflicts(
        Dictionary<string, IReadOnlyList<ContentDigest>> currentByCandidate)
    {
        var currentRecordDigests = currentByCandidate.Values.SelectMany(item => item).ToHashSet();
        var currentRecords = _records.Where(record => currentRecordDigests.Contains(record.Digest)).ToArray();

        var result = new Dictionary<string, IReadOnlyList<ExtractionConflict>>(StringComparer.Ordinal);
        foreach (var candidateGroup in currentRecords.GroupBy(item => item.FormCandidateId, StringComparer.Ordinal))
        {
            var conflicts = new List<ExtractionConflict>();

            var byField = candidateGroup
                .SelectMany(record => record.Values.Select(value => new { record.Digest, value }))
                .GroupBy(item => item.value.FieldId, StringComparer.Ordinal)
                .ToArray();

            foreach (var fieldGroup in byField)
            {
                var bySignature = fieldGroup
                    .GroupBy(entry => ValueSignature(entry.value), StringComparer.Ordinal)
                    .ToArray();

                if (bySignature.Length <= 1)
                {
                    continue;
                }

                var signatures = bySignature.Select(group => group.Key).OrderBy(item => item, StringComparer.Ordinal).ToArray();
                var sourceRecordDigests = bySignature
                    .SelectMany(group => group.Select(entry => entry.Digest))
                    .Distinct()
                    .OrderBy(item => item.ToString(), StringComparer.Ordinal)
                    .ToArray();

                var seed = $"{candidateGroup.Key}\u001f{fieldGroup.Key}\u001f{string.Join("|", signatures)}";
                var conflictId = $"extraction-conflict-{ContentDigest.Sha256Utf8(seed).Value[..16]}";

                var conflict = new ExtractionConflict(
                    conflictId,
                    candidateGroup.Key,
                    fieldGroup.Key,
                    sourceRecordDigests,
                    IsResolved(
                        candidateGroup.Key,
                        fieldGroup.Key,
                        sourceRecordDigests,
                        conflictId));
                conflicts.Add(conflict);
            }

            if (conflicts.Count > 0)
            {
                result[candidateGroup.Key] = conflicts.AsReadOnly();
            }
        }

        return result;
    }

    private bool IsResolved(
        string candidateId,
        string fieldId,
        IReadOnlyList<ContentDigest> sourceRecordDigests,
        string conflictId)
    {
        return _records.Any(record =>
            record.Kind == ExtractionRecordKind.Resolution &&
            string.Equals(record.FormCandidateId, candidateId, StringComparison.Ordinal) &&
            record.SourceConflictId is not null &&
            string.Equals(record.SourceConflictId, conflictId, StringComparison.Ordinal) &&
            record.Values.Any(value => string.Equals(value.FieldId, fieldId, StringComparison.Ordinal)) &&
            SameDigestSet(record.SourceRecordDigests, sourceRecordDigests));
    }

    private ExtractionProjection RecomputeProjection()
    {
        var currentByCandidate = CurrentRecordSet();
        var conflicts = BuildConflicts(currentByCandidate);

        return new ExtractionProjection(
            _entries.Count == 0 ? Form.Digest : _entries[^1].Digest,
            currentByCandidate,
            conflicts,
            GetInvalidatedRecordDigests().ToArray());
    }

    private HashSet<ContentDigest> GetInvalidatedRecordDigests()
    {
        return _invalidations
            .SelectMany(invalidation => invalidation.AffectedRecordDigests)
            .ToHashSet();
    }

    private static string ValueSignature(ExtractionFieldValue value) =>
        CanonicalJsonSerializer.Serialize(
            new CanonicalJsonObject()
                .Add("field_id", value.FieldId)
                .Add("value", CanonicalJsonValue.DeepClone(value.Value))
                .Add("evidence_location_digest", value.EvidenceLocation.Digest.ToString()));

    private static bool SameDigestSet(IReadOnlyList<ContentDigest> left, IReadOnlyList<ContentDigest> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var rightSet = right.Select(item => item.ToString()).ToHashSet(StringComparer.Ordinal);
        return left.All(item => rightSet.Contains(item.ToString()));
    }

    private ExtractionRecord? FindRecord(ContentDigest digest)
    {
        foreach (var record in _records)
        {
            if (record.Digest == digest)
            {
                return record;
            }
        }

        return null;
    }
}
