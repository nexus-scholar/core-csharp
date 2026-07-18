using System.Globalization;
using System.Linq;
using System.Text;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.CorpusSnapshots;

public static class CorpusSnapshotService
{
    private const string NoIdReason = "no-stable-identifier";

    public static VerifiedCorpusSnapshot CreateBaseline(
        string snapshotId,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        string createdByActorId,
        string createdByRole,
        IClock clock)
    {
        if (sourceResult is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Verified source result is required.");
        }

        if (policy is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Authority policy is required.");
        }

        if (clock is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Clock is required.");
        }

        var normalizedSnapshotId = RequireCanonicalText(snapshotId, nameof(snapshotId));
        var normalizedActorId = RequireCanonicalText(createdByActorId, nameof(createdByActorId));
        var normalizedActorRole = RequireCanonicalText(createdByRole, nameof(createdByRole));

        if (!string.Equals(normalizedActorId, policy.IssuedByActorId, StringComparison.Ordinal) ||
            !string.Equals(normalizedActorRole, policy.IssuedByRole, StringComparison.Ordinal))
        {
            throw Invalid(CorpusSnapshotErrorCodes.UnauthorizedPublisher, "Snapshot publisher must be the policy issuer.");
        }

        if (!policy.ContainsAuthorizedActor(normalizedActorId, normalizedActorRole))
        {
            throw Invalid(
                CorpusSnapshotErrorCodes.UnauthorizedPublisher,
                "Snapshot publisher is not authorized by the active policy.");
        }

        var source = BuildSourceMaterial(sourceResult);
        var expectedCreatedAt = RequireCanonicalUtc(clock.UtcNow, nameof(clock.UtcNow));

        var sourceCandidateIds = source.CandidatesById.Keys.ToHashSet(StringComparer.Ordinal);
        var noIdCandidateIds = sourceCandidateIds
            .Where(candidateId => !source.CandidatesById[candidateId].HasStableIdentifier)
            .ToHashSet(StringComparer.Ordinal);
        var noIdCandidates = noIdCandidateIds
            .Select(candidateId => source.CandidatesById[candidateId])
            .ToArray();

        var noIdUnresolved = noIdCandidates
            .Select(candidate => BuildUnresolvedCandidate(candidate))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();

        var groups = source.Groups
            .Select(group => new CorpusSnapshotGroup(
                group.GroupId,
                group.RepresentativeCandidateId,
                group.MemberCandidateIds.ToArray(),
                group.EvidenceReferences.OrderBy(reference => reference.Kind, StringComparer.Ordinal)
                    .ThenBy(reference => reference.EvidenceId, StringComparer.Ordinal)
                    .ThenBy(reference => reference.DigestScope, StringComparer.Ordinal)
                    .ThenBy(reference => reference.Digest.ToString(), StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(group => group.GroupId, StringComparer.Ordinal)
            .ToArray();
        var decisionSetDigest = ComputeDecisionSetDigest(Array.Empty<CorpusSnapshotDecisionReference>());

        var normalized = new NormalizedCorpusSnapshot(
            normalizedSnapshotId,
            source.ResultId,
            sourceResult.ResultDigest,
            Array.Empty<CorpusSnapshotDecisionReference>(),
            decisionSetDigest,
            groups,
            noIdUnresolved,
            normalizedActorId,
            normalizedActorRole,
            policy.PolicyId,
            policy.PolicyDigest,
            expectedCreatedAt,
            (null, null),
            Array.Empty<CorpusSnapshotInvalidationReference>());

        ValidateSnapshotMaterial(
            source,
            normalized,
            inputDecisionRefsMustExist: false,
            validateSupersession: true);

        var contentMaterial = BuildSnapshotContent(normalized, canonicalizeCollections: true);
        var contentEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            contentMaterial);
        var contentDigest = contentEnvelope.ComputeDigest();

        var recordMaterial = BuildSnapshotRecord(normalized, contentDigest, canonicalizeCollections: true);
        var recordEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            recordMaterial);
        var recordDigest = recordEnvelope.ComputeDigest();

        return new VerifiedCorpusSnapshot(
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            normalized.SnapshotId,
            normalized.SourceResultId,
            normalized.SourceResultDigest,
            normalized.DecisionReferences,
            normalized.DecisionSetDigest,
            normalized.Groups,
            normalized.UnresolvedCandidates,
            normalized.CreatedByActorId,
            normalized.CreatedByRole,
            normalized.AuthoritySourceId,
            normalized.AuthoritySourceDigest,
            normalized.CreatedAt,
            normalized.SupersedesSnapshotId,
            normalized.SupersedesSnapshotRecordDigest,
            normalized.InvalidationReferences,
            contentDigest,
            recordDigest,
            contentEnvelope,
            recordEnvelope);
    }

    public static VerifiedCorpusSnapshot Rehydrate(
        UnverifiedCorpusSnapshot input,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy)
    {
        if (input is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot material is required.");
        }

        if (sourceResult is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Verified source result is required.");
        }

        if (policy is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Authority policy is required.");
        }

        EnsureKnownSchema(input);
        var source = BuildSourceMaterial(sourceResult);

        var snapshotId = RequireCanonicalText(input.SnapshotId, nameof(input.SnapshotId));
        var sourceResultId = RequireCanonicalText(input.SourceResultId, nameof(input.SourceResultId));
        var sourceResultDigest = RequireValidDigest(input.SourceResultDigest, nameof(input.SourceResultDigest));

        if (!string.Equals(sourceResultId, source.ResultId, StringComparison.Ordinal) ||
            sourceResultDigest != sourceResult.ResultDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.StaleSourceBinding, "Snapshot source binding does not match the verified source result.");
        }

        var authoritySourceId = RequireCanonicalText(input.AuthoritySourceId, nameof(input.AuthoritySourceId));
        var authoritySourceDigest = RequireValidDigest(input.AuthoritySourceDigest, nameof(input.AuthoritySourceDigest));
        if (!string.Equals(authoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
            authoritySourceDigest != policy.PolicyDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.StaleSourceBinding, "Snapshot authority source binding does not match the active policy.");
        }

        var actorId = RequireCanonicalText(input.CreatedByActorId, nameof(input.CreatedByActorId));
        var actorRole = RequireCanonicalText(input.CreatedByRole, nameof(input.CreatedByRole));
        if (!policy.ContainsAuthorizedActor(actorId, actorRole))
        {
            throw Invalid(CorpusSnapshotErrorCodes.UnauthorizedPublisher, "Snapshot publisher must match an authorized policy actor.");
        }

        var createdAt = RequireCanonicalUtc(input.CreatedAt, nameof(input.CreatedAt));

        var decisionRefs = NormalizeDecisionReferences(input.DecisionReferences);
        var decisionSetDigest = ComputeDecisionSetDigest(decisionRefs);
        if (!RequireValidDigest(input.DecisionSetDigest, nameof(input.DecisionSetDigest)).Equals(decisionSetDigest))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot decision-set digest does not match decision references.");
        }

        var groups = NormalizeGroups(input.Groups);
        var unresolved = NormalizeUnresolvedCandidates(input.UnresolvedCandidates);
        var invalidations = NormalizeInvalidationReferences(input.InvalidationReferences);
        if (invalidations.Count != 0 || decisionRefs.Count != 0 || input.SupersedesSnapshotId is not null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Baseline snapshots cannot include successors or validation material.");
        }

        var normalized = new NormalizedCorpusSnapshot(
            snapshotId,
            source.ResultId,
            sourceResult.ResultDigest,
            decisionRefs,
            decisionSetDigest,
            groups,
            unresolved,
            actorId,
            actorRole,
            authoritySourceId,
            authoritySourceDigest,
            createdAt,
            NormalizeOptionalSupersession(input.SupersedesSnapshotId, input.SupersedesSnapshotRecordDigest),
            invalidations);

        ValidateSnapshotMaterial(
            source,
            normalized,
            inputDecisionRefsMustExist: false,
            validateSupersession: true);

        var providedContent = BuildSnapshotContent(normalized, canonicalizeCollections: false);
        var canonicalContent = BuildSnapshotContent(normalized, canonicalizeCollections: true);
        EnsureCanonicalOrdering("snapshot content", providedContent, canonicalContent);

        var expectedContentDigest = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonicalContent).ComputeDigest();

        var providedContentDigest = input.ContentDigest is { IsValid: true } contentDigest
            ? contentDigest
            : throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot content digest is required.");
        if (expectedContentDigest != providedContentDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot content digest does not match snapshot content.");
        }

        var expectedRecordEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            BuildSnapshotRecord(normalized, expectedContentDigest, canonicalizeCollections: true));

        var providedRecord = BuildSnapshotRecord(normalized, expectedContentDigest, canonicalizeCollections: false);
        var canonicalRecord = BuildSnapshotRecord(normalized, expectedContentDigest, canonicalizeCollections: true);

        EnsureCanonicalOrdering("snapshot record", providedRecord, canonicalRecord);

        var expectedRecordDigest = expectedRecordEnvelope.ComputeDigest();
        var providedRecordDigest = input.RecordDigest is { IsValid: true } recordDigest
            ? recordDigest
            : throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot record digest is required.");
        if (expectedRecordDigest != providedRecordDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot record digest does not match snapshot record material.");
        }

        var contentEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonicalContent);

        var recordEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonicalRecord);

        return new VerifiedCorpusSnapshot(
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            normalized.SnapshotId,
            normalized.SourceResultId,
            normalized.SourceResultDigest,
            normalized.DecisionReferences,
            normalized.DecisionSetDigest,
            normalized.Groups,
            normalized.UnresolvedCandidates,
            normalized.CreatedByActorId,
            normalized.CreatedByRole,
            normalized.AuthoritySourceId,
            normalized.AuthoritySourceDigest,
            normalized.CreatedAt,
            normalized.SupersedesSnapshotId,
            normalized.SupersedesSnapshotRecordDigest,
            normalized.InvalidationReferences,
            expectedContentDigest,
            expectedRecordDigest,
            contentEnvelope,
            recordEnvelope);
    }

    public static VerifiedCorpusSnapshot CreateSuccessor(
        string snapshotId,
        VerifiedCorpusSnapshot predecessorSnapshot,
        VerifiedDeduplicationAuthorityPolicy policy,
        string createdByActorId,
        string createdByRole,
        IClock clock,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> activeDecisions,
        IReadOnlyList<CorpusSnapshotInvalidationReference> invalidationReferences,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot> knownSnapshots,
        VerifiedDeduplicationAuthorityResultDigest? sourceResult = null,
        VerifiedDeduplicationAuthorityDecision? decisionToApply = null)
    {
        if (snapshotId is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot id is required.");
        }

        if (predecessorSnapshot is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Predecessor snapshot is required.");
        }

        if (policy is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Authority policy is required.");
        }

        if (clock is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Clock is required.");
        }

        if (activeDecisions is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Active decisions are required.");
        }

        if (invalidationReferences is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Invalidation references are required.");
        }

        if (knownDecisions is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Known decisions are required.");
        }

        if (knownSnapshots is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Known snapshots are required.");
        }

        RequireNoNullEntries(activeDecisions, "Active decisions cannot contain null entries.");
        RequireNoNullEntries(knownDecisions, "Known decisions cannot contain null entries.");
        RequireNoNullEntries(knownSnapshots, "Known snapshots cannot contain null entries.");

        if (!string.Equals(predecessorSnapshot.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
            predecessorSnapshot.AuthoritySourceDigest != policy.PolicyDigest)
        {
            throw Invalid(
                CorpusSnapshotErrorCodes.StaleSourceBinding,
                "Successor predecessor snapshot must be published by active policy.");
        }

        var actorId = RequireCanonicalText(createdByActorId, nameof(createdByActorId));
        var actorRole = RequireCanonicalText(createdByRole, nameof(createdByRole));
        if (!policy.ContainsAuthorizedActor(actorId, actorRole))
        {
            throw Invalid(CorpusSnapshotErrorCodes.UnauthorizedPublisher, "Snapshot publisher must be authorized by the active policy.");
        }

        var orderedActiveDecisions = activeDecisions
            .OrderBy(decision => decision.DecisionId, StringComparer.Ordinal)
            .ToArray();
        var decisionRefs = orderedActiveDecisions
            .Select(decision => new CorpusSnapshotDecisionReference(decision.DecisionId, decision.DecisionDigest))
            .ToArray();
        var decisionSetDigest = ComputeDecisionSetDigest(decisionRefs);
        var normalizedInvalidations = NormalizeInvalidationReferences(invalidationReferences);

        ValidateSuccessorSnapshotReferences(
            decisionRefs,
            decisionSetDigest,
            normalizedInvalidations,
            predecessorSnapshot,
            activeDecisions,
            knownDecisions,
            knownSnapshots,
            policy,
            decisionToApply is not null);

        if ((sourceResult is null) != (decisionToApply is null))
        {
            throw Invalid(
                CorpusSnapshotErrorCodes.InvalidSnapshot,
                "Reduced successor creation requires both the verified source result and decision to apply.");
        }

        var reduction = sourceResult is null
            ? null
            : DeduplicationSnapshotReducer.Reduce(
                sourceResult,
                predecessorSnapshot,
                decisionToApply!,
                activeDecisions,
                knownDecisions);

        var normalized = new NormalizedCorpusSnapshot(
            RequireCanonicalText(snapshotId, nameof(snapshotId)),
            predecessorSnapshot.SourceResultId,
            predecessorSnapshot.SourceResultDigest,
            decisionRefs,
            decisionSetDigest,
            reduction?.Groups ?? predecessorSnapshot.Groups,
            reduction?.UnresolvedCandidates ?? predecessorSnapshot.UnresolvedCandidates,
            actorId,
            actorRole,
            policy.PolicyId,
            policy.PolicyDigest,
            RequireCanonicalUtc(clock.UtcNow, nameof(clock.UtcNow)),
            (predecessorSnapshot.SnapshotId, predecessorSnapshot.RecordDigest),
            normalizedInvalidations);

        var contentMaterial = BuildSnapshotContent(normalized, canonicalizeCollections: true);
        var contentEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            contentMaterial);
        var contentDigest = contentEnvelope.ComputeDigest();

        var recordMaterial = BuildSnapshotRecord(normalized, contentDigest, canonicalizeCollections: true);
        var recordEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            recordMaterial);
        var recordDigest = recordEnvelope.ComputeDigest();

        return new VerifiedCorpusSnapshot(
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            normalized.SnapshotId,
            normalized.SourceResultId,
            normalized.SourceResultDigest,
            normalized.DecisionReferences,
            normalized.DecisionSetDigest,
            normalized.Groups,
            normalized.UnresolvedCandidates,
            normalized.CreatedByActorId,
            normalized.CreatedByRole,
            normalized.AuthoritySourceId,
            normalized.AuthoritySourceDigest,
            normalized.CreatedAt,
            normalized.SupersedesSnapshotId,
            normalized.SupersedesSnapshotRecordDigest,
            normalized.InvalidationReferences,
            contentDigest,
            recordDigest,
            contentEnvelope,
            recordEnvelope);
    }

    public static VerifiedCorpusSnapshot RehydrateSuccessor(
        UnverifiedCorpusSnapshot input,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedCorpusSnapshot predecessorSnapshot,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> activeDecisions,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot> knownSnapshots,
        VerifiedDeduplicationAuthorityDecision? decisionToApply = null)
    {
        if (input is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot material is required.");
        }

        if (sourceResult is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Verified source result is required.");
        }

        if (policy is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Authority policy is required.");
        }

        if (predecessorSnapshot is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Predecessor snapshot is required.");
        }

        if (activeDecisions is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Active decisions are required.");
        }

        if (knownDecisions is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Known decisions are required.");
        }

        if (knownSnapshots is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Known snapshots are required.");
        }

        if (!string.Equals(predecessorSnapshot.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
            predecessorSnapshot.AuthoritySourceDigest != policy.PolicyDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.StaleSourceBinding, "Predecessor snapshot is not published by active policy.");
        }

        EnsureKnownSchema(input);
        var source = BuildSourceMaterial(sourceResult);

        var snapshotId = RequireCanonicalText(input.SnapshotId, nameof(input.SnapshotId));
        var sourceResultId = RequireCanonicalText(input.SourceResultId, nameof(input.SourceResultId));
        var sourceResultDigest = RequireValidDigest(input.SourceResultDigest, nameof(input.SourceResultDigest));

        if (!string.Equals(sourceResultId, source.ResultId, StringComparison.Ordinal) ||
            sourceResultDigest != sourceResult.ResultDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.StaleSourceBinding, "Snapshot source binding does not match the verified source result.");
        }

        var authoritySourceId = RequireCanonicalText(input.AuthoritySourceId, nameof(input.AuthoritySourceId));
        var authoritySourceDigest = RequireValidDigest(input.AuthoritySourceDigest, nameof(input.AuthoritySourceDigest));
        if (!string.Equals(authoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
            authoritySourceDigest != policy.PolicyDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.StaleSourceBinding, "Snapshot authority source binding does not match the active policy.");
        }

        var actorId = RequireCanonicalText(input.CreatedByActorId, nameof(input.CreatedByActorId));
        var actorRole = RequireCanonicalText(input.CreatedByRole, nameof(input.CreatedByRole));
        if (!policy.ContainsAuthorizedActor(actorId, actorRole))
        {
            throw Invalid(CorpusSnapshotErrorCodes.UnauthorizedPublisher, "Snapshot publisher must match an authorized policy actor.");
        }

        var createdAt = RequireCanonicalUtc(input.CreatedAt, nameof(input.CreatedAt));

        var decisionRefs = NormalizeDecisionReferences(input.DecisionReferences);
        var decisionSetDigest = ComputeDecisionSetDigest(decisionRefs);
        if (!RequireValidDigest(input.DecisionSetDigest, nameof(input.DecisionSetDigest)).Equals(decisionSetDigest))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot decision-set digest does not match decision references.");
        }

        var groups = NormalizeGroups(input.Groups);
        var unresolved = NormalizeUnresolvedCandidates(input.UnresolvedCandidates);
        var invalidations = NormalizeInvalidationReferences(input.InvalidationReferences);

        var successor = new NormalizedCorpusSnapshot(
            snapshotId,
            source.ResultId,
            sourceResult.ResultDigest,
            decisionRefs,
            decisionSetDigest,
            groups,
            unresolved,
            actorId,
            actorRole,
            authoritySourceId,
            authoritySourceDigest,
            createdAt,
            NormalizeOptionalSupersession(input.SupersedesSnapshotId, input.SupersedesSnapshotRecordDigest),
            invalidations);

        if (successor.SupersedesSnapshotId is null ||
            successor.SupersedesSnapshotRecordDigest is null ||
            successor.SupersedesSnapshotRecordDigest != predecessorSnapshot.RecordDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor snapshot must supersede the active predecessor snapshot.");
        }

        if (!string.Equals(successor.SupersedesSnapshotId, predecessorSnapshot.SnapshotId, StringComparison.Ordinal))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor snapshot predecessor id does not match active snapshot.");
        }

        if (successor.DecisionReferences.Count == 0)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor snapshots must include active decision references.");
        }

        if (successor.InvalidationReferences.Count == 0)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor snapshots must include invalidation references.");
        }

        ValidateSnapshotMaterial(
            source,
            successor,
            inputDecisionRefsMustExist: true,
            validateSupersession: true,
            requireSourceGroupMembership: decisionToApply is null);
        ValidateSuccessorSnapshotReferences(
            successor.DecisionReferences,
            decisionSetDigest,
            successor.InvalidationReferences,
            predecessorSnapshot,
            activeDecisions,
            knownDecisions,
            knownSnapshots,
            policy,
            decisionToApply is not null);

        if (decisionToApply is not null)
        {
            var expected = DeduplicationSnapshotReducer.Reduce(
                sourceResult,
                predecessorSnapshot,
                decisionToApply,
                activeDecisions,
                knownDecisions);
            EnsureReducedMembershipMatches(successor, expected);
        }

        var providedContent = BuildSnapshotContent(successor, canonicalizeCollections: false);
        var canonicalContent = BuildSnapshotContent(successor, canonicalizeCollections: true);
        EnsureCanonicalOrdering("snapshot content", providedContent, canonicalContent);

        var expectedContentDigest = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonicalContent).ComputeDigest();

        var providedContentDigest = input.ContentDigest is { IsValid: true } contentDigest
            ? contentDigest
            : throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot content digest is required.");
        if (expectedContentDigest != providedContentDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot content digest does not match snapshot content.");
        }

        var expectedRecordEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            BuildSnapshotRecord(successor, expectedContentDigest, canonicalizeCollections: true));

        var providedRecord = BuildSnapshotRecord(successor, expectedContentDigest, canonicalizeCollections: false);
        var canonicalRecord = BuildSnapshotRecord(successor, expectedContentDigest, canonicalizeCollections: true);

        EnsureCanonicalOrdering("snapshot record", providedRecord, canonicalRecord);

        var expectedRecordDigest = expectedRecordEnvelope.ComputeDigest();
        var providedRecordDigest = input.RecordDigest is { IsValid: true } recordDigest
            ? recordDigest
            : throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot record digest is required.");
        if (expectedRecordDigest != providedRecordDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot record digest does not match snapshot record material.");
        }

        var contentEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonicalContent);

        var recordEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonicalRecord);

        return new VerifiedCorpusSnapshot(
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            successor.SnapshotId,
            successor.SourceResultId,
            successor.SourceResultDigest,
            successor.DecisionReferences,
            successor.DecisionSetDigest,
            successor.Groups,
            successor.UnresolvedCandidates,
            successor.CreatedByActorId,
            successor.CreatedByRole,
            successor.AuthoritySourceId,
            successor.AuthoritySourceDigest,
            successor.CreatedAt,
            successor.SupersedesSnapshotId,
            successor.SupersedesSnapshotRecordDigest,
            successor.InvalidationReferences,
            expectedContentDigest,
            expectedRecordDigest,
            contentEnvelope,
            recordEnvelope);
    }

    private sealed class NormalizedSourceMaterial(
        string resultId,
        IReadOnlyDictionary<string, DedupCandidateRecord> candidatesById,
        IReadOnlyList<CorpusSnapshotGroup> groups,
        IReadOnlyList<CorpusSnapshotUnresolvedCandidate> unresolved)
    {
        public string ResultId { get; } = resultId;
        public IReadOnlyDictionary<string, DedupCandidateRecord> CandidatesById { get; } = candidatesById;
        public IReadOnlyList<CorpusSnapshotGroup> Groups { get; } = groups;
        public IReadOnlyList<CorpusSnapshotUnresolvedCandidate> UnresolvedCandidates { get; } = unresolved;
    }

    private sealed class NormalizedCorpusSnapshot(
        string snapshotId,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        IReadOnlyList<CorpusSnapshotDecisionReference> decisionReferences,
        ContentDigest decisionSetDigest,
        IReadOnlyList<CorpusSnapshotGroup> groups,
        IReadOnlyList<CorpusSnapshotUnresolvedCandidate> unresolvedCandidates,
        string createdByActorId,
        string createdByRole,
        string authoritySourceId,
        ContentDigest authoritySourceDigest,
        DateTimeOffset createdAt,
        (string? SupersedesSnapshotId, ContentDigest? SupersedesSnapshotRecordDigest) supersession,
        IReadOnlyList<CorpusSnapshotInvalidationReference> invalidationReferences)
    {
        public string SnapshotId { get; } = snapshotId;
        public string SourceResultId { get; } = sourceResultId;
        public ContentDigest SourceResultDigest { get; } = sourceResultDigest;
        public IReadOnlyList<CorpusSnapshotDecisionReference> DecisionReferences { get; } = Array.AsReadOnly(decisionReferences.ToArray());
        public ContentDigest DecisionSetDigest { get; } = decisionSetDigest;
        public IReadOnlyList<CorpusSnapshotGroup> Groups { get; } = Array.AsReadOnly(groups.ToArray());
        public IReadOnlyList<CorpusSnapshotUnresolvedCandidate> UnresolvedCandidates { get; } = Array.AsReadOnly(unresolvedCandidates.ToArray());
        public string CreatedByActorId { get; } = createdByActorId;
        public string CreatedByRole { get; } = createdByRole;
        public string AuthoritySourceId { get; } = authoritySourceId;
        public ContentDigest AuthoritySourceDigest { get; } = authoritySourceDigest;
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public string? SupersedesSnapshotId { get; } = supersession.SupersedesSnapshotId;
        public ContentDigest? SupersedesSnapshotRecordDigest { get; } = supersession.SupersedesSnapshotRecordDigest;
        public IReadOnlyList<CorpusSnapshotInvalidationReference> InvalidationReferences { get; } = Array.AsReadOnly(
            invalidationReferences.ToArray());
    }

    private static NormalizedSourceMaterial BuildSourceMaterial(VerifiedDeduplicationAuthorityResultDigest sourceResult)
    {
        var result = sourceResult.Result ?? throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Verified source result is required.");

        var candidatesById = result.RawCandidates.ToDictionary(
            item => RequireCanonicalText(item.CandidateId, nameof(item.CandidateId)),
            item => item,
            StringComparer.Ordinal);

        var groupedCandidateIds = new HashSet<string>(StringComparer.Ordinal);
        var groups = result.Clusters.Select(cluster => NormalizeClusterAsGroup(cluster, candidatesById, groupedCandidateIds)).ToArray();

        var allStableCandidateIds = candidatesById
            .Where(item => item.Value.HasStableIdentifier)
            .Select(item => item.Key)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var singletonGroups = allStableCandidateIds
            .Where(candidateId => !groupedCandidateIds.Contains(candidateId))
            .Select(candidateId => new CorpusSnapshotGroup(
                BuildGroupId(new[] { candidateId }),
                candidateId,
                new[] { candidateId },
                Array.Empty<CorpusSnapshotEvidenceReference>()))
            .ToArray();

        var unresolved = candidatesById
            .Where(item => !item.Value.HasStableIdentifier)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => BuildUnresolvedCandidate(item.Value))
            .ToArray();

        var allGroups = groups.Concat(singletonGroups).ToArray();

        return new NormalizedSourceMaterial(result.ResultId, candidatesById, allGroups, unresolved);
    }

    private static CorpusSnapshotGroup NormalizeClusterAsGroup(
        DedupCluster cluster,
        IReadOnlyDictionary<string, DedupCandidateRecord> sourceCandidates,
        HashSet<string> groupedCandidateIds)
    {
        var candidateIds = cluster.Members
            .Select(member => RequireCanonicalText(member.CandidateId, nameof(member.CandidateId)))
            .ToArray();

        if (candidateIds.Length == 0)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot groups must contain at least one member.");
        }

        if (candidateIds.Distinct(StringComparer.Ordinal).Count() != candidateIds.Length)
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Snapshot group members must be unique.");
        }

        var orderedCandidates = candidateIds.OrderBy(candidateId => candidateId, StringComparer.Ordinal).ToArray();
        foreach (var candidateId in orderedCandidates)
        {
            if (!sourceCandidates.ContainsKey(candidateId) || !sourceCandidates[candidateId].HasStableIdentifier)
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot groups must contain only stable source candidates.");
            }

            if (!groupedCandidateIds.Add(candidateId))
            {
                throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "A source candidate may belong to only one group.");
            }
        }

        var representativeId = RequireCanonicalText(cluster.Representative.CandidateId, nameof(cluster.Representative.CandidateId));
        if (!candidateIds.Contains(representativeId, StringComparer.Ordinal))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Group representative must be one of the group members.");
        }

        var evidence = cluster.Evidence
            .Select(evidence => NormalizeEvidenceReference(evidence))
            .ToArray();

        if (evidence.Length != evidence.Distinct(new CorpusSnapshotEvidenceReferenceComparer()).Count())
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Group evidence references must be unique.");
        }

        return new CorpusSnapshotGroup(
            BuildGroupId(orderedCandidates),
            representativeId,
            orderedCandidates,
            evidence);
    }

    private static CorpusSnapshotUnresolvedCandidate BuildUnresolvedCandidate(DedupCandidateRecord candidate)
    {
        if (candidate.HasStableIdentifier)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Only no-id candidates may be unresolved.");
        }

        var rawSightingRefs = new[] { RequireCanonicalText(candidate.Source.SourceSightingId, "source sighting id") }
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        return new CorpusSnapshotUnresolvedCandidate(
            RequireCanonicalText(candidate.CandidateId, nameof(candidate.CandidateId)),
            NoIdReason,
            rawSightingRefs,
            DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(candidate).CandidateDigest);
    }

    private static CorpusSnapshotEvidenceReference NormalizeEvidenceReference(DedupEvidence evidence)
    {
        var evidenceId = RequireCanonicalText(evidence.EvidenceId, nameof(evidence.EvidenceId));
        var digest = DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(evidence).EvidenceDigest;
        return new CorpusSnapshotEvidenceReference(evidence.Kind.ToString(), evidenceId, DigestScope.CanonicalJsonRecord.Value, digest);
    }

    private static IReadOnlyList<CorpusSnapshotDecisionReference> NormalizeDecisionReferences(IReadOnlyList<CorpusSnapshotDecisionReference> references)
    {
        if (references is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot decision references are required.");
        }

        RequireNoNullEntries(references, "Snapshot decision references cannot contain null entries.");

        var normalized = references
            .Select(reference => new CorpusSnapshotDecisionReference(
                RequireCanonicalText(reference.DecisionId, nameof(reference.DecisionId)),
                RequireValidDigest(reference.DecisionDigest, nameof(reference.DecisionDigest))))
            .ToArray();

        if (normalized.Select(item => item.DecisionId).Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Decision references must be unique by decision id.");
        }

        return normalized;
    }

    private static IReadOnlyList<CorpusSnapshotGroup> NormalizeGroups(IReadOnlyList<CorpusSnapshotGroup> groups)
    {
        if (groups is null || groups.Count == 0)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot groups are required.");
        }

        RequireNoNullEntries(groups, "Snapshot groups cannot contain null entries.");
        foreach (var group in groups)
        {
            if (group.MemberCandidateIds is null || group.EvidenceReferences is null)
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot group collections are required.");
            }

            RequireNoNullEntries(group.EvidenceReferences, "Snapshot evidence references cannot contain null entries.");
        }

        var normalized = groups.Select(group =>
            new CorpusSnapshotGroup(
                RequireCanonicalText(group.GroupId, nameof(group.GroupId)),
                RequireCanonicalText(group.RepresentativeCandidateId, nameof(group.RepresentativeCandidateId)),
                group.MemberCandidateIds.Select(id => RequireCanonicalText(id, nameof(id))).ToArray(),
                group.EvidenceReferences.Select(reference => NormalizeSnapshotEvidenceReference(reference)).ToArray())).ToArray();

        return normalized;
    }

    private static CorpusSnapshotEvidenceReference NormalizeSnapshotEvidenceReference(CorpusSnapshotEvidenceReference reference)
    {
        if (!DigestScope.TryParse(RequireCanonicalText(reference.DigestScope, nameof(reference.DigestScope)), out var scope) ||
            scope != DigestScope.CanonicalJsonRecord)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot evidence digest scope must be canonical-json-record.");
        }

        return new CorpusSnapshotEvidenceReference(
            RequireCanonicalText(reference.Kind, nameof(reference.Kind)),
            RequireCanonicalText(reference.EvidenceId, nameof(reference.EvidenceId)),
            scope.Value,
            RequireValidDigest(reference.Digest, nameof(reference.Digest)));
    }

    private static IReadOnlyList<CorpusSnapshotUnresolvedCandidate> NormalizeUnresolvedCandidates(
        IReadOnlyList<CorpusSnapshotUnresolvedCandidate> unresolved)
    {
        if (unresolved is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot unresolved candidates are required.");
        }

        RequireNoNullEntries(unresolved, "Snapshot unresolved candidates cannot contain null entries.");
        foreach (var candidate in unresolved)
        {
            if (candidate.RawSightingReferences is null)
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Unresolved raw-sighting references are required.");
            }
        }

        var normalized = unresolved.Select(candidate => new CorpusSnapshotUnresolvedCandidate(
            RequireCanonicalText(candidate.CandidateId, nameof(candidate.CandidateId)),
            RequireCanonicalText(candidate.UnresolvedReason, nameof(candidate.UnresolvedReason)),
            candidate.RawSightingReferences
                .Select(reference => RequireCanonicalText(reference, nameof(candidate.RawSightingReferences)))
                .ToArray(),
            RequireValidDigest(candidate.CandidateContentDigest, nameof(candidate.CandidateContentDigest)))).ToArray();

        return normalized;
    }

    private static IReadOnlyList<CorpusSnapshotInvalidationReference> NormalizeInvalidationReferences(
        IReadOnlyList<CorpusSnapshotInvalidationReference> invalidationReferences)
    {
        if (invalidationReferences is null)
        {
            return Array.Empty<CorpusSnapshotInvalidationReference>();
        }

        RequireNoNullEntries(invalidationReferences, "Snapshot invalidation references cannot contain null entries.");

        return invalidationReferences.Select(reference =>
            new CorpusSnapshotInvalidationReference(
                RequireCanonicalText(reference.RecordKind, nameof(reference.RecordKind)),
                RequireCanonicalText(reference.RecordId, nameof(reference.RecordId)),
                RequireValidDigest(reference.RecordDigest, nameof(reference.RecordDigest)))).ToArray();
    }

    private static (string? SnapshotId, ContentDigest? RecordDigest) NormalizeOptionalSupersession(
        string? snapshotId,
        ContentDigest? recordDigest)
    {
        var hasId = !string.IsNullOrWhiteSpace(snapshotId);
        var hasDigest = recordDigest is { IsValid: true };
        if (hasId != hasDigest)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot supersession requires both identifier and digest.");
        }

        return !hasId
            ? (null, null)
            : (RequireCanonicalText(snapshotId!, nameof(snapshotId)), recordDigest);
    }

    private static void ValidateSnapshotMaterial(
        NormalizedSourceMaterial source,
        NormalizedCorpusSnapshot normalized,
        bool inputDecisionRefsMustExist,
        bool validateSupersession,
        bool requireSourceGroupMembership = true)
    {
        if (inputDecisionRefsMustExist && normalized.DecisionReferences.Count == 0)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot decision references must be present for persisted snapshots.");
        }

        var sourceCandidateIds = source.CandidatesById.Keys.ToHashSet(StringComparer.Ordinal);
        if (normalized.SupersedesSnapshotId is not null && !validateSupersession)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot supersession is not valid for this snapshot context.");
        }

        var unresolvedCandidateIds = normalized.UnresolvedCandidates.Select(candidate => candidate.CandidateId).ToArray();
        if (unresolvedCandidateIds.Distinct(StringComparer.Ordinal).Count() != unresolvedCandidateIds.Length)
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Snapshot unresolved candidate identifiers must be unique.");
        }

        if (source.UnresolvedCandidates.Select(item => item.CandidateId).Distinct(StringComparer.Ordinal).Count() !=
            source.UnresolvedCandidates.Count)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Source unresolved candidates must be unique.");
        }

        foreach (var unresolved in normalized.UnresolvedCandidates)
        {
            if (!source.CandidatesById.TryGetValue(unresolved.CandidateId, out var sourceCandidate))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot unresolved candidate is not present in source result.");
            }

            if (sourceCandidate.HasStableIdentifier)
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Only no-id candidates may remain unresolved.");
            }

            if (!string.Equals(unresolved.UnresolvedReason, NoIdReason, StringComparison.Ordinal))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Unresolved no-id candidates must use the stable unresolved reason.");
            }

            var expectedDigest = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(sourceCandidate).CandidateDigest;
            if (unresolved.CandidateContentDigest != expectedDigest)
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Unresolved candidate digest does not match the source result candidate.");
            }

            var expectedSources = new[] { sourceCandidate.Source.SourceSightingId };
            if (!unresolved.RawSightingReferences.SequenceEqual(expectedSources, StringComparer.Ordinal))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Unresolved raw-sighting references do not match source result.");
            }
        }

        var groupedCandidateIds = normalized.Groups
            .SelectMany(group => group.MemberCandidateIds)
            .ToArray();
        if (groupedCandidateIds.Distinct(StringComparer.Ordinal).Count() != groupedCandidateIds.Length)
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "A source candidate may appear in only one snapshot group.");
        }
        var unresolvedIdSet = unresolvedCandidateIds.ToHashSet(StringComparer.Ordinal);
        var groupedIdSet = groupedCandidateIds.ToHashSet(StringComparer.Ordinal);

        if (sourceCandidateIds.Count != groupedIdSet.Count + unresolvedIdSet.Count ||
            !groupedIdSet.IsSupersetOf(sourceCandidateIds.Except(unresolvedIdSet, StringComparer.Ordinal).ToArray()) ||
            !unresolvedIdSet.IsSubsetOf(sourceCandidateIds))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Every source candidate must appear exactly once as a grouped member or unresolved candidate.");
        }

        if (normalized.DecisionReferences.Select(reference => reference.DecisionId).Distinct(StringComparer.Ordinal).Count() !=
            normalized.DecisionReferences.Count)
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Decision references must be unique by decision id.");
        }

        var sourceGroupLookup = source.Groups.ToDictionary(group => group.GroupId, StringComparer.Ordinal);
        if (normalized.Groups.Select(group => group.GroupId).Distinct(StringComparer.Ordinal).Count() != normalized.Groups.Count)
        {
            throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Snapshot group identifiers must be unique.");
        }

        foreach (var normalizedGroup in normalized.Groups)
        {
            var orderedMembers = normalizedGroup.MemberCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (orderedMembers.Length == 0 ||
                !string.Equals(normalizedGroup.GroupId, BuildGroupId(orderedMembers), StringComparison.Ordinal) ||
                !orderedMembers.Contains(normalizedGroup.RepresentativeCandidateId, StringComparer.Ordinal) ||
                orderedMembers.Any(candidateId => !source.CandidatesById.ContainsKey(candidateId)))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot group identity, membership, or representative is invalid.");
            }

            if (!requireSourceGroupMembership)
            {
                continue;
            }

            if (!sourceGroupLookup.TryGetValue(normalizedGroup.GroupId, out var sourceGroup))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot group is not present in the bound source result.");
            }

            var expectedMembers = sourceGroup.MemberCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var providedMembers = normalizedGroup.MemberCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (!expectedMembers.SequenceEqual(providedMembers))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot group members do not match the bound source cluster membership.");
            }

            if (!string.Equals(normalizedGroup.RepresentativeCandidateId, sourceGroup.RepresentativeCandidateId, StringComparison.Ordinal))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot representative does not match source cluster representative.");
            }

            var expectedEvidence = sourceGroup.EvidenceReferences.OrderBy(reference => reference, new CanonicalEvidenceReferenceComparer());
            var providedEvidence = normalizedGroup.EvidenceReferences.OrderBy(reference => reference, new CanonicalEvidenceReferenceComparer());
            if (!expectedEvidence.Select(reference => reference.WithSortKey()).SequenceEqual(
                providedEvidence.Select(reference => reference.WithSortKey())))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot group evidence references are not fully reproducible from source result.");
            }
        }
    }

    private static void EnsureReducedMembershipMatches(
        NormalizedCorpusSnapshot successor,
        DeduplicationSnapshotReduction expected)
    {
        if (successor.Groups.Count != expected.Groups.Count ||
            successor.UnresolvedCandidates.Count != expected.UnresolvedCandidates.Count)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor membership does not match the verified decision reduction.");
        }

        for (var index = 0; index < expected.Groups.Count; index++)
        {
            var actual = successor.Groups[index];
            var required = expected.Groups[index];
            if (!string.Equals(actual.GroupId, required.GroupId, StringComparison.Ordinal) ||
                !string.Equals(actual.RepresentativeCandidateId, required.RepresentativeCandidateId, StringComparison.Ordinal) ||
                !actual.MemberCandidateIds.SequenceEqual(required.MemberCandidateIds, StringComparer.Ordinal) ||
                !actual.EvidenceReferences.Select(reference => reference.WithSortKey())
                    .SequenceEqual(required.EvidenceReferences.Select(reference => reference.WithSortKey()), StringComparer.Ordinal))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor groups do not match the verified decision reduction.");
            }
        }

        for (var index = 0; index < expected.UnresolvedCandidates.Count; index++)
        {
            var actual = successor.UnresolvedCandidates[index];
            var required = expected.UnresolvedCandidates[index];
            if (!string.Equals(actual.CandidateId, required.CandidateId, StringComparison.Ordinal) ||
                !string.Equals(actual.UnresolvedReason, required.UnresolvedReason, StringComparison.Ordinal) ||
                actual.CandidateContentDigest != required.CandidateContentDigest ||
                !actual.RawSightingReferences.SequenceEqual(required.RawSightingReferences, StringComparer.Ordinal))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor unresolved membership does not match the verified decision reduction.");
            }
        }
    }

    private static void ValidateSuccessorSnapshotReferences(
        IReadOnlyList<CorpusSnapshotDecisionReference> activeDecisionRefs,
        ContentDigest decisionSetDigest,
        IReadOnlyList<CorpusSnapshotInvalidationReference> invalidationRefs,
        VerifiedCorpusSnapshot predecessorSnapshot,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> activeDecisions,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot> knownSnapshots,
        VerifiedDeduplicationAuthorityPolicy policy,
        bool requireActiveInvalidatedDecision)
    {
        if (activeDecisionRefs is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor decision references are required.");
        }

        if (invalidationRefs is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor invalidation references are required.");
        }

        if (activeDecisions is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Successor active decisions are required.");
        }

        if (knownDecisions is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Known decisions are required.");
        }

        if (knownSnapshots is null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Known snapshots are required.");
        }

        RequireNoNullEntries(activeDecisions, "Active decisions cannot contain null entries.");
        RequireNoNullEntries(knownDecisions, "Known decisions cannot contain null entries.");
        RequireNoNullEntries(knownSnapshots, "Known snapshots cannot contain null entries.");

        var orderedActiveDecisionRefs = activeDecisions
            .OrderBy(decision => decision.DecisionId, StringComparer.Ordinal)
            .Select(decision => new CorpusSnapshotDecisionReference(decision.DecisionId, decision.DecisionDigest))
            .ToArray();
        ValidateActiveDecisionSet(activeDecisions, knownDecisions, predecessorSnapshot, policy);
        var expectedDecisionSetDigest = ComputeDecisionSetDigest(orderedActiveDecisionRefs);
        if (expectedDecisionSetDigest != decisionSetDigest || expectedDecisionSetDigest != ComputeDecisionSetDigest(activeDecisionRefs))
        {
            throw Invalid(
                CorpusSnapshotErrorCodes.InvalidSnapshot,
                "Successor snapshot decision references must match exactly the active decisions.");
        }

        if (orderedActiveDecisionRefs.Length != activeDecisionRefs.Count)
        {
            throw Invalid(
                CorpusSnapshotErrorCodes.InvalidSnapshot,
                "Successor snapshot decision references must match the active decision set.");
        }

        var knownDecisionRefs = knownDecisions
            .ToDictionary(item => $"{item.DecisionId}\u001f{item.DecisionDigest}", StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value);
        var knownSnapshotRefs = knownSnapshots
            .ToDictionary(item => $"{item.SnapshotId}\u001f{item.RecordDigest}", StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value);

        foreach (var reference in activeDecisionRefs)
        {
            var decisionKey = $"{reference.DecisionId}\u001f{reference.DecisionDigest}";
            if (!knownDecisionRefs.ContainsKey(decisionKey))
            {
                throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Active snapshot decision references must resolve to verified decisions.");
            }

            var knownDecision = knownDecisionRefs[decisionKey];
            if (!string.Equals(knownDecision.SourceResultId, predecessorSnapshot.SourceResultId, StringComparison.Ordinal) ||
                knownDecision.SourceResultDigest != predecessorSnapshot.SourceResultDigest ||
                !string.Equals(knownDecision.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
                knownDecision.AuthoritySourceDigest != policy.PolicyDigest ||
                !ResolvesSourceSnapshot(knownDecision, predecessorSnapshot, knownSnapshots))
            {
                throw Invalid(
                    CorpusSnapshotErrorCodes.InvalidSnapshot,
                    "Successor decision references must match the active policy and source result lineage.");
            }
        }

        var normalizedInvalidationRefs = invalidationRefs.Select(reference =>
        {
            var kind = RequireCanonicalText(reference.RecordKind, nameof(reference.RecordKind));
            var recordId = RequireCanonicalText(reference.RecordId, nameof(reference.RecordId));
            var recordDigest = RequireValidDigest(reference.RecordDigest, nameof(reference.RecordDigest));
            var key = $"{recordId}\u001f{recordDigest}";

            if (!string.Equals(kind, CorpusSnapshotInvalidationConstants.InvalidationDecisionKind, StringComparison.Ordinal) &&
                !string.Equals(kind, CorpusSnapshotInvalidationConstants.InvalidationSnapshotKind, StringComparison.Ordinal))
            {
                throw Invalid(
                    CorpusSnapshotErrorCodes.InvalidSnapshot,
                    "Invalidation reference kinds are restricted to deduplication-decision and corpus-snapshot.");
            }

            if (string.Equals(kind, CorpusSnapshotInvalidationConstants.InvalidationDecisionKind, StringComparison.Ordinal))
            {
                if (!knownDecisionRefs.TryGetValue(key, out var invalidatedDecision) ||
                    !string.Equals(invalidatedDecision.SourceResultId, predecessorSnapshot.SourceResultId, StringComparison.Ordinal) ||
                    invalidatedDecision.SourceResultDigest != predecessorSnapshot.SourceResultDigest ||
                    !string.Equals(invalidatedDecision.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
                    invalidatedDecision.AuthoritySourceDigest != policy.PolicyDigest ||
                    (requireActiveInvalidatedDecision
                        ? !predecessorSnapshot.DecisionReferences.Any(reference =>
                            string.Equals(reference.DecisionId, invalidatedDecision.DecisionId, StringComparison.Ordinal) &&
                            reference.DecisionDigest == invalidatedDecision.DecisionDigest)
                        : !string.Equals(invalidatedDecision.SourceSnapshotId, predecessorSnapshot.SnapshotId, StringComparison.Ordinal) ||
                          invalidatedDecision.SourceSnapshotRecordDigest != predecessorSnapshot.RecordDigest))
                {
                    throw Invalid(
                        CorpusSnapshotErrorCodes.InvalidSnapshot,
                        "Invalidation references must resolve to known verified decisions.");
                }
            }
            else
            {
                if (!knownSnapshotRefs.TryGetValue(key, out var invalidatedSnapshot) ||
                    !string.Equals(invalidatedSnapshot.SnapshotId, predecessorSnapshot.SnapshotId, StringComparison.Ordinal) ||
                    invalidatedSnapshot.RecordDigest != predecessorSnapshot.RecordDigest ||
                    !string.Equals(invalidatedSnapshot.SourceResultId, predecessorSnapshot.SourceResultId, StringComparison.Ordinal) ||
                    invalidatedSnapshot.SourceResultDigest != predecessorSnapshot.SourceResultDigest ||
                    !string.Equals(invalidatedSnapshot.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
                    invalidatedSnapshot.AuthoritySourceDigest != policy.PolicyDigest)
                {
                    throw Invalid(
                        CorpusSnapshotErrorCodes.InvalidSnapshot,
                        "Invalidation references must resolve to known verified snapshots.");
                }
            }

            return key;
        }).ToArray();

        if (normalizedInvalidationRefs.Distinct(StringComparer.Ordinal).Count() != normalizedInvalidationRefs.Length)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Invalidation references must be unique by kind,id,digest.");
        }
    }

    private static bool ResolvesSourceSnapshot(
        VerifiedDeduplicationAuthorityDecision decision,
        VerifiedCorpusSnapshot predecessorSnapshot,
        IReadOnlyList<VerifiedCorpusSnapshot> knownSnapshots)
    {
        if (decision.SourceSnapshotId is null || decision.SourceSnapshotRecordDigest is null)
        {
            return false;
        }

        var knownByRecord = knownSnapshots
            .Append(predecessorSnapshot)
            .GroupBy(snapshot => $"{snapshot.SnapshotId}\u001f{snapshot.RecordDigest}", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = predecessorSnapshot;
        while (visited.Add($"{current.SnapshotId}\u001f{current.RecordDigest}"))
        {
            if (string.Equals(decision.SourceSnapshotId, current.SnapshotId, StringComparison.Ordinal) &&
                decision.SourceSnapshotRecordDigest == current.RecordDigest)
            {
                return true;
            }

            if (current.SupersedesSnapshotId is null || current.SupersedesSnapshotRecordDigest is null ||
                !knownByRecord.TryGetValue($"{current.SupersedesSnapshotId}\u001f{current.SupersedesSnapshotRecordDigest}", out current))
            {
                return false;
            }
        }

        return false;
    }

    private static void ValidateActiveDecisionSet(
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> activeDecisions,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> knownDecisions,
        VerifiedCorpusSnapshot predecessorSnapshot,
        VerifiedDeduplicationAuthorityPolicy policy)
    {
        var knownById = new Dictionary<string, VerifiedDeduplicationAuthorityDecision>(StringComparer.Ordinal);
        foreach (var decision in knownDecisions)
        {
            if (!knownById.TryAdd(decision.DecisionId, decision))
            {
                throw Invalid(CorpusSnapshotErrorCodes.DuplicateSnapshotMaterial, "Known decisions must have unique decision ids.");
            }
        }

        var activeIds = activeDecisions.Select(item => item.DecisionId).ToHashSet(StringComparer.Ordinal);
        var duplicateTarget = activeDecisions
            .GroupBy(item => $"{item.PolicyId}\u001f{item.TargetKind}\u001f{item.TargetId}", StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTarget is not null)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Only one active decision is allowed per policy target.");
        }

        foreach (var decision in activeDecisions)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { decision.DecisionId };
            var current = decision;
            while (current.SupersedesDecisionId is not null)
            {
                if (activeIds.Contains(current.SupersedesDecisionId))
                {
                    throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "An active decision set cannot retain a superseded decision.");
                }

                if (!knownById.TryGetValue(current.SupersedesDecisionId, out var superseded))
                {
                    throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Decision supersession must resolve to a known verified decision.");
                }

                if (!visited.Add(superseded.DecisionId))
                {
                    throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Decision supersession chains must be acyclic.");
                }

                if (!string.Equals(superseded.PolicyId, policy.PolicyId, StringComparison.Ordinal) ||
                    !string.Equals(superseded.TargetKind, decision.TargetKind, StringComparison.Ordinal) ||
                    !string.Equals(superseded.TargetId, decision.TargetId, StringComparison.Ordinal) ||
                    !string.Equals(superseded.SourceResultId, predecessorSnapshot.SourceResultId, StringComparison.Ordinal) ||
                    superseded.SourceResultDigest != predecessorSnapshot.SourceResultDigest)
                {
                    throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Decision supersession must remain on the same policy, target, and source lineage.");
                }

                current = superseded;
            }
        }
    }

    private static void RequireNoNullEntries<T>(IEnumerable<T> values, string message) where T : class
    {
        if (values.Any(value => value is null))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, message);
        }
    }

    private static ContentDigest ComputeDecisionSetDigest(IReadOnlyList<CorpusSnapshotDecisionReference> decisionRefs)
    {
        var canonical = BuildDecisionSetMaterial(decisionRefs.OrderBy(reference => reference.DecisionId, StringComparer.Ordinal));
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotConstants.SchemaId,
            CorpusSnapshotConstants.SchemaVersion,
            canonical);
        return envelope.ComputeDigest();
    }

    private static CanonicalJsonObject BuildDecisionSetMaterial(IEnumerable<CorpusSnapshotDecisionReference> decisionRefs)
    {
        return new CanonicalJsonObject().Add("decision_references",
            CanonicalJsonValue.Array(decisionRefs
                .Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("decision_id", item.DecisionId)
                    .Add("decision_digest", item.DecisionDigest.ToString()))
                .ToArray()));
    }

    private static CanonicalJsonObject BuildSnapshotContent(NormalizedCorpusSnapshot normalized, bool canonicalizeCollections)
    {
        var decisionRefs = canonicalizeCollections
            ? normalized.DecisionReferences.OrderBy(reference => reference.DecisionId, StringComparer.Ordinal).ToArray()
            : normalized.DecisionReferences.ToArray();
        var groups = canonicalizeCollections
            ? normalized.Groups.OrderBy(group => group.GroupId, StringComparer.Ordinal).ToArray()
            : normalized.Groups.ToArray();
        var unresolved = canonicalizeCollections
            ? normalized.UnresolvedCandidates.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray()
            : normalized.UnresolvedCandidates.ToArray();

        return new CanonicalJsonObject()
            .Add("schema_id", CorpusSnapshotConstants.SchemaId)
            .Add("schema_version", CorpusSnapshotConstants.SchemaVersion)
            .Add("source_result_id", normalized.SourceResultId)
            .Add("source_result_digest", normalized.SourceResultDigest.ToString())
            .Add("decision_references", CanonicalJsonValue.Array(decisionRefs
                .Select(reference => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("decision_id", reference.DecisionId)
                    .Add("decision_digest", reference.DecisionDigest.ToString()))
                .ToArray()))
            .Add("decision_set_digest", normalized.DecisionSetDigest.ToString())
            .Add("groups", CanonicalJsonValue.Array(groups.Select(group =>
            {
                var memberCandidateIds = canonicalizeCollections
                    ? group.MemberCandidateIds.OrderBy(candidate => candidate, StringComparer.Ordinal).ToArray()
                    : group.MemberCandidateIds.ToArray();
                var evidenceReferences = canonicalizeCollections
                    ? group.EvidenceReferences
                        .OrderBy(reference => reference.Kind, StringComparer.Ordinal)
                        .ThenBy(reference => reference.EvidenceId, StringComparer.Ordinal)
                        .ThenBy(reference => reference.DigestScope, StringComparer.Ordinal)
                        .ThenBy(reference => reference.Digest.ToString(), StringComparer.Ordinal)
                        .ToArray()
                    : group.EvidenceReferences.ToArray();

                return (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("group_id", group.GroupId)
                    .Add("representative_candidate_id", group.RepresentativeCandidateId)
                    .Add("member_candidate_ids", CanonicalJsonValue.Array(
                        memberCandidateIds.Select(CanonicalJsonValue.From).ToArray()))
                    .Add("evidence_references", CanonicalJsonValue.Array(evidenceReferences.Select(BuildEvidenceReferenceMaterial).ToArray()));
            }).ToArray()))
            .Add("unresolved_candidates", CanonicalJsonValue.Array(unresolved.Select(candidate =>
                (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("candidate_id", candidate.CandidateId)
                    .Add("unresolved_reason", candidate.UnresolvedReason)
                    .Add("raw_sighting_references", CanonicalJsonValue.Array(
                        (canonicalizeCollections
                            ? candidate.RawSightingReferences.OrderBy(reference => reference, StringComparer.Ordinal).ToArray()
                            : candidate.RawSightingReferences.ToArray())
                            .Select(CanonicalJsonValue.From).ToArray()))
                    .Add("candidate_content_digest", candidate.CandidateContentDigest.ToString())).ToArray()));
    }

    private static CanonicalJsonObject BuildSnapshotRecord(
        NormalizedCorpusSnapshot normalized,
        ContentDigest contentDigest,
        bool canonicalizeCollections)
    {
        var decisionRefs = canonicalizeCollections
            ? normalized.DecisionReferences.OrderBy(reference => reference.DecisionId, StringComparer.Ordinal).ToArray()
            : normalized.DecisionReferences.ToArray();
        var groups = canonicalizeCollections
            ? normalized.Groups.OrderBy(group => group.GroupId, StringComparer.Ordinal).ToArray()
            : normalized.Groups.ToArray();
        var unresolved = canonicalizeCollections
            ? normalized.UnresolvedCandidates.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray()
            : normalized.UnresolvedCandidates.ToArray();
        var invalidations = canonicalizeCollections
            ? normalized.InvalidationReferences
                .OrderBy(reference => reference.RecordKind, StringComparer.Ordinal)
                .ThenBy(reference => reference.RecordId, StringComparer.Ordinal)
                .ThenBy(reference => reference.RecordDigest.ToString(), StringComparer.Ordinal)
                .ToArray()
            : normalized.InvalidationReferences.ToArray();

        var builder = new CanonicalJsonObject()
            .Add("snapshot_id", normalized.SnapshotId)
            .Add("schema_id", CorpusSnapshotConstants.SchemaId)
            .Add("schema_version", CorpusSnapshotConstants.SchemaVersion)
            .Add("source_result_id", normalized.SourceResultId)
            .Add("source_result_digest", normalized.SourceResultDigest.ToString())
            .Add("decision_references", CanonicalJsonValue.Array(decisionRefs
                .Select(reference => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("decision_id", reference.DecisionId)
                    .Add("decision_digest", reference.DecisionDigest.ToString()))
                .ToArray()))
            .Add("decision_set_digest", normalized.DecisionSetDigest.ToString())
            .Add("groups", CanonicalJsonValue.Array(groups.Select(group =>
            {
                var memberCandidateIds = canonicalizeCollections
                    ? group.MemberCandidateIds.OrderBy(candidate => candidate, StringComparer.Ordinal).ToArray()
                    : group.MemberCandidateIds.ToArray();
                var evidenceReferences = canonicalizeCollections
                    ? group.EvidenceReferences
                        .OrderBy(reference => reference.Kind, StringComparer.Ordinal)
                        .ThenBy(reference => reference.EvidenceId, StringComparer.Ordinal)
                        .ThenBy(reference => reference.DigestScope, StringComparer.Ordinal)
                        .ThenBy(reference => reference.Digest.ToString(), StringComparer.Ordinal)
                        .ToArray()
                    : group.EvidenceReferences.ToArray();

                return (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("group_id", group.GroupId)
                    .Add("representative_candidate_id", group.RepresentativeCandidateId)
                    .Add("member_candidate_ids", CanonicalJsonValue.Array(
                        memberCandidateIds.Select(CanonicalJsonValue.From).ToArray()))
                    .Add("evidence_references", CanonicalJsonValue.Array(evidenceReferences.Select(BuildEvidenceReferenceMaterial).ToArray()));
            }).ToArray()))
            .Add("unresolved_candidates", CanonicalJsonValue.Array(unresolved.Select(candidate =>
                (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("candidate_id", candidate.CandidateId)
                    .Add("unresolved_reason", candidate.UnresolvedReason)
                    .Add("raw_sighting_references", CanonicalJsonValue.Array(
                        (canonicalizeCollections
                            ? candidate.RawSightingReferences.OrderBy(reference => reference, StringComparer.Ordinal).ToArray()
                            : candidate.RawSightingReferences.ToArray())
                            .Select(CanonicalJsonValue.From).ToArray()))
                    .Add("candidate_content_digest", candidate.CandidateContentDigest.ToString())).ToArray()))
            .Add("created_by_actor_id", normalized.CreatedByActorId)
            .Add("created_by_role", normalized.CreatedByRole)
            .Add("authority_source_id", normalized.AuthoritySourceId)
            .Add("authority_source_digest", normalized.AuthoritySourceDigest.ToString())
            .AddTimestamp("created_at", normalized.CreatedAt)
            .Add("content_digest", contentDigest.ToString())
            .Add("invalidation_references", CanonicalJsonValue.Array(invalidations.Select(reference =>
                (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("record_kind", reference.RecordKind)
                    .Add("record_id", reference.RecordId)
                    .Add("record_digest", reference.RecordDigest.ToString()))
                .ToArray()));

        if (normalized.SupersedesSnapshotId is not null)
        {
            builder = builder
                .Add("supersedes_snapshot_id", normalized.SupersedesSnapshotId)
                .Add("supersedes_snapshot_record_digest", normalized.SupersedesSnapshotRecordDigest!.Value.ToString());
        }

        return builder;
    }

    private static CanonicalJsonObject BuildEvidenceReferenceMaterial(CorpusSnapshotEvidenceReference reference)
    {
        return new CanonicalJsonObject()
            .Add("kind", reference.Kind)
            .Add("evidence_id", reference.EvidenceId)
            .Add("digest_scope", reference.DigestScope)
            .Add("digest", reference.Digest.ToString());
    }

    private static string BuildGroupId(IReadOnlyList<string> orderedMemberCandidateIds)
    {
        var normalized = orderedMemberCandidateIds
            .OrderBy(candidateId => candidateId, StringComparer.Ordinal)
            .ToArray();
        var payload = new CanonicalJsonObject()
            .Add("member_candidate_ids", CanonicalJsonValue.Array(normalized.Select(CanonicalJsonValue.From).ToArray()));
        return $"group-{ContentDigest.Sha256CanonicalJson(payload).Value}";
    }

    private static void EnsureCanonicalOrdering(
        string label,
        CanonicalJsonValue provided,
        CanonicalJsonValue canonical)
    {
        if (!string.Equals(
                CanonicalJsonSerializer.Serialize(provided),
                CanonicalJsonSerializer.Serialize(canonical),
                StringComparison.Ordinal))
        {
            throw Invalid(CorpusSnapshotErrorCodes.NonCanonicalSnapshot, $"{label} is not in canonical collection order.");
        }
    }

    private static string RequireCanonicalText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, $"{name} is required.");
        }

        if (!value.IsNormalized(NormalizationForm.FormC))
        {
            throw Invalid(CorpusSnapshotErrorCodes.NonCanonicalSnapshot, $"{name} must be NFC-normalized.");
        }

        return value;
    }

    private static ContentDigest RequireValidDigest(ContentDigest value, string name)
    {
        if (!value.IsValid)
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, $"{name} must be a valid content digest.");
        }

        return value;
    }

    private static DateTimeOffset RequireCanonicalUtc(DateTimeOffset value, string name)
    {
        if (!CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, $"{name} must be canonical UTC.");
        }

        return value;
    }

    private static void EnsureKnownSchema(UnverifiedCorpusSnapshot input)
    {
        if (!string.Equals(input.SchemaId, CorpusSnapshotConstants.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(input.SchemaVersion, CorpusSnapshotConstants.SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(CorpusSnapshotErrorCodes.InvalidSnapshot, "Snapshot schema id or version is invalid.");
        }
    }

    private static CorpusSnapshotAuthorityException Invalid(string category, string message) =>
        new CorpusSnapshotAuthorityException(category, message);

    private sealed class CorpusSnapshotEvidenceReferenceComparer : IEqualityComparer<CorpusSnapshotEvidenceReference>
    {
        public bool Equals(CorpusSnapshotEvidenceReference? x, CorpusSnapshotEvidenceReference? y) =>
            string.Equals(x?.Kind, y?.Kind, StringComparison.Ordinal) &&
            string.Equals(x?.EvidenceId, y?.EvidenceId, StringComparison.Ordinal) &&
            string.Equals(x?.DigestScope, y?.DigestScope, StringComparison.Ordinal) &&
            x?.Digest == y?.Digest;

        public int GetHashCode(CorpusSnapshotEvidenceReference obj) =>
            HashCode.Combine(obj.Kind, obj.EvidenceId, obj.DigestScope, obj.Digest);
    }

    private sealed class CanonicalEvidenceReferenceComparer : IComparer<CorpusSnapshotEvidenceReference>
    {
        public int Compare(CorpusSnapshotEvidenceReference? x, CorpusSnapshotEvidenceReference? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var kind = StringComparer.Ordinal.Compare(x.Kind, y.Kind);
            if (kind != 0)
            {
                return kind;
            }

            var evidence = StringComparer.Ordinal.Compare(x.EvidenceId, y.EvidenceId);
            if (evidence != 0)
            {
                return evidence;
            }

            var scope = StringComparer.Ordinal.Compare(x.DigestScope, y.DigestScope);
            if (scope != 0)
            {
                return scope;
            }

            return StringComparer.Ordinal.Compare(x.Digest.ToString(), y.Digest.ToString());
        }
    }
}

file static class CorpusSnapshotExtensions
{
    public static string WithSortKey(this CorpusSnapshotEvidenceReference reference) =>
        $"{reference.Kind}\u001f{reference.EvidenceId}\u001f{reference.DigestScope}\u001f{reference.Digest}";
}
