using System.Text.Json;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Screening.CorpusSnapshots;

public static class ScreeningCorpusBindingConstants
{
    public const string SchemaId = "nexus.screening.corpus-binding";
    public const string SchemaVersion = "1.0.0";
}

public static class ScreeningCorpusBindingErrorCodes
{
    public const string InvalidBinding = "invalid-screening-corpus-binding";
    public const string StaleSourceBinding = "stale-screening-corpus-binding";
    public const string MembershipMismatch = "screening-corpus-membership-mismatch";
    public const string DuplicateUnit = "duplicate-screening-corpus-unit";
    public const string NonCanonicalBinding = "non-canonical-screening-corpus-binding";
}

public sealed class ScreeningCorpusBindingException : InvalidOperationException
{
    public ScreeningCorpusBindingException(string category, string message) : base(message) => Category = category;

    public string Category { get; }
}

public sealed record ScreeningCorpusGroupUnit(
    string GroupId,
    string ScreeningCandidateId,
    IReadOnlyList<string> MemberCandidateIds);

public sealed record ScreeningCorpusUnresolvedUnit(
    string ScreeningCandidateId,
    ContentDigest CandidateDigest);

public sealed record UnverifiedScreeningCorpusBinding(
    string SchemaId,
    string SchemaVersion,
    string BindingId,
    string SourceResultId,
    ContentDigest SourceResultDigest,
    string SnapshotId,
    ContentDigest SnapshotRecordDigest,
    ContentDigest DecisionSetDigest,
    IReadOnlyList<ScreeningCorpusGroupUnit> GroupUnits,
    IReadOnlyList<ScreeningCorpusUnresolvedUnit> UnresolvedUnits,
    ContentDigest BindingDigest);

public sealed class VerifiedScreeningCorpusBinding
{
    internal VerifiedScreeningCorpusBinding(
        string bindingId,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        string snapshotId,
        ContentDigest snapshotRecordDigest,
        ContentDigest decisionSetDigest,
        IReadOnlyList<ScreeningCorpusGroupUnit> groupUnits,
        IReadOnlyList<ScreeningCorpusUnresolvedUnit> unresolvedUnits,
        DigestEnvelope envelope)
    {
        BindingId = bindingId;
        SourceResultId = sourceResultId;
        SourceResultDigest = sourceResultDigest;
        SnapshotId = snapshotId;
        SnapshotRecordDigest = snapshotRecordDigest;
        DecisionSetDigest = decisionSetDigest;
        GroupUnits = Array.AsReadOnly(groupUnits.Select(item => item with
        {
            MemberCandidateIds = Array.AsReadOnly(item.MemberCandidateIds.ToArray())
        }).ToArray());
        UnresolvedUnits = Array.AsReadOnly(unresolvedUnits.Select(item => item with { }).ToArray());
        DigestEnvelope = envelope;
    }

    public string BindingId { get; }
    public string SourceResultId { get; }
    public ContentDigest SourceResultDigest { get; }
    public string SnapshotId { get; }
    public ContentDigest SnapshotRecordDigest { get; }
    public ContentDigest DecisionSetDigest { get; }
    public IReadOnlyList<ScreeningCorpusGroupUnit> GroupUnits { get; }
    public IReadOnlyList<ScreeningCorpusUnresolvedUnit> UnresolvedUnits { get; }
    public DigestEnvelope DigestEnvelope { get; }
    public ContentDigest BindingDigest => DigestEnvelope.ComputeDigest();

    public IReadOnlyList<string> ScreeningCandidateIds => Array.AsReadOnly(
        GroupUnits.Select(item => item.ScreeningCandidateId)
            .Concat(UnresolvedUnits.Select(item => item.ScreeningCandidateId))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray());

    public CanonicalJsonObject ToCanonicalJson() => DigestEnvelope.ToCanonicalJsonObject();
}

public sealed class VerifiedSnapshotBoundScreeningPolicy
{
    internal VerifiedSnapshotBoundScreeningPolicy(
        VerifiedScreeningCorpusBinding binding,
        ScreeningConductPolicy policy)
    {
        Binding = binding;
        Policy = policy;
    }

    public VerifiedScreeningCorpusBinding Binding { get; }
    public ScreeningConductPolicy Policy { get; }
}

public static class ScreeningCorpusBindingAuthority
{
    public static VerifiedScreeningCorpusBinding Create(
        string bindingId,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedCorpusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(sourceResult);
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureSourceBinding(sourceResult, snapshot);

        var sourceById = BuildSourceIndex(sourceResult);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groupUnits = snapshot.Groups
            .Select(group =>
            {
                var groupId = RequireText(group.GroupId, nameof(group.GroupId));
                var representativeId = RequireText(group.RepresentativeCandidateId, nameof(group.RepresentativeCandidateId));
                var members = group.MemberCandidateIds.Select(item => RequireText(item, nameof(group.MemberCandidateIds))).ToArray();
                if (members.Length == 0 || !members.Contains(representativeId, StringComparer.Ordinal))
                    throw Invalid(ScreeningCorpusBindingErrorCodes.MembershipMismatch, "Snapshot group representative must be one of its members.");
                foreach (var member in members)
                    AddExactMember(member, sourceById, seen);
                return new ScreeningCorpusGroupUnit(groupId, representativeId, Array.AsReadOnly(members));
            })
            .OrderBy(item => item.GroupId, StringComparer.Ordinal)
            .ToArray();

        var unresolvedUnits = snapshot.UnresolvedCandidates
            .Select(item =>
            {
                var candidateId = RequireText(item.CandidateId, nameof(item.CandidateId));
                AddExactMember(candidateId, sourceById, seen);
                var expected = DeduplicationAuthorityDigests.CreateCandidateDigestMaterial(sourceById[candidateId]).CandidateDigest;
                if (item.CandidateContentDigest != expected)
                    throw Invalid(ScreeningCorpusBindingErrorCodes.MembershipMismatch, "Snapshot unresolved candidate digest does not match source authority.");
                return new ScreeningCorpusUnresolvedUnit(candidateId, expected);
            })
            .OrderBy(item => item.ScreeningCandidateId, StringComparer.Ordinal)
            .ToArray();

        if (seen.Count != sourceById.Count)
            throw Invalid(ScreeningCorpusBindingErrorCodes.MembershipMismatch, "Snapshot membership does not cover the exact source candidate set.");

        return BuildVerified(
            RequireText(bindingId, nameof(bindingId)),
            sourceResult.Result.ResultId,
            sourceResult.ResultDigest,
            snapshot.SnapshotId,
            snapshot.RecordDigest,
            snapshot.DecisionSetDigest,
            groupUnits,
            unresolvedUnits);
    }

    public static VerifiedScreeningCorpusBinding Rehydrate(
        UnverifiedScreeningCorpusBinding input,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedCorpusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!string.Equals(input.SchemaId, ScreeningCorpusBindingConstants.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(input.SchemaVersion, ScreeningCorpusBindingConstants.SchemaVersion, StringComparison.Ordinal))
            throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, "Unknown Screening corpus binding schema.");

        var expected = Create(input.BindingId, sourceResult, snapshot);
        if (!string.Equals(input.SourceResultId, expected.SourceResultId, StringComparison.Ordinal) ||
            input.SourceResultDigest != expected.SourceResultDigest ||
            !string.Equals(input.SnapshotId, expected.SnapshotId, StringComparison.Ordinal) ||
            input.SnapshotRecordDigest != expected.SnapshotRecordDigest ||
            input.DecisionSetDigest != expected.DecisionSetDigest)
            throw Invalid(ScreeningCorpusBindingErrorCodes.StaleSourceBinding, "Persisted Screening corpus source binding is stale.");

        var provided = BuildContent(
            input.BindingId,
            input.SourceResultId,
            input.SourceResultDigest,
            input.SnapshotId,
            input.SnapshotRecordDigest,
            input.DecisionSetDigest,
            input.GroupUnits,
            input.UnresolvedUnits);
        var expectedBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(expected.DigestEnvelope.Content);
        var providedBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(provided);
        if (!providedBytes.SequenceEqual(expectedBytes))
            throw Invalid(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, "Persisted Screening corpus units are missing, altered, duplicated, or not canonically ordered.");
        if (input.BindingDigest != expected.BindingDigest)
            throw Invalid(ScreeningCorpusBindingErrorCodes.StaleSourceBinding, "Persisted Screening corpus binding digest is stale.");
        return expected;
    }

    public static VerifiedSnapshotBoundScreeningPolicy CreateConductPolicy(
        VerifiedScreeningCorpusBinding binding,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        string policyId,
        string candidateSetId,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria,
        int requiredReviewCount,
        IEnumerable<ScreeningConductRoleAssignment> assignments,
        IEnumerable<string>? adjudicatorRoles,
        IEnumerable<ScreeningExclusionReason>? exclusionReasons,
        ScreeningConductActor approvedBy,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(sourceResult);
        if (!string.Equals(binding.SourceResultId, sourceResult.Result.ResultId, StringComparison.Ordinal) ||
            binding.SourceResultDigest != sourceResult.ResultDigest)
            throw Invalid(ScreeningCorpusBindingErrorCodes.StaleSourceBinding, "Binding does not match the verified source result.");

        var sourceById = BuildSourceIndex(sourceResult);
        var candidates = binding.ScreeningCandidateIds.Select(id =>
            sourceById.TryGetValue(id, out var candidate)
                ? candidate
                : throw Invalid(ScreeningCorpusBindingErrorCodes.MembershipMismatch, "Binding Screening unit is absent from source authority."))
            .ToArray();
        var sourceRefs = new[]
        {
            $"deduplication-result:{binding.SourceResultId}:{binding.SourceResultDigest}",
            $"corpus-snapshot:{binding.SnapshotId}:{binding.SnapshotRecordDigest}",
            $"corpus-decision-set:{binding.DecisionSetDigest}",
            $"screening-corpus-binding:{binding.BindingId}:{binding.BindingDigest}"
        }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var candidateSet = new ScreeningCandidateSet(
            ScreeningSchema.CandidateSetSchemaId,
            ScreeningSchema.CandidateSetSchemaVersion,
            RequireText(candidateSetId, nameof(candidateSetId)),
            ScreeningSourceKinds.LockedReviewableCandidateSet,
            Array.AsReadOnly(sourceRefs),
            true,
            binding.SourceResultId,
            binding.SourceResultDigest.ToString(),
            Array.AsReadOnly(candidates),
            Array.Empty<DedupCandidateRecord>(),
            sourceResult.Result.NonClaims);
        var policy = ScreeningConductPolicy.CreateFromVerifiedCandidateSet(
            policyId,
            candidateSet,
            protocol,
            criteria,
            requiredReviewCount,
            assignments,
            adjudicatorRoles,
            exclusionReasons,
            approvedBy,
            approvedAt);
        return VerifyConductPolicyBinding(binding, policy);
    }

    public static VerifiedSnapshotBoundScreeningPolicy VerifyConductPolicyBinding(
        VerifiedScreeningCorpusBinding binding,
        ScreeningConductPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(policy);
        var expectedIds = binding.ScreeningCandidateIds;
        var actualIds = policy.CandidateSet.Candidates.Select(item => item.CandidateId).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var expectedRefs = new[]
        {
            $"deduplication-result:{binding.SourceResultId}:{binding.SourceResultDigest}",
            $"corpus-snapshot:{binding.SnapshotId}:{binding.SnapshotRecordDigest}",
            $"corpus-decision-set:{binding.DecisionSetDigest}",
            $"screening-corpus-binding:{binding.BindingId}:{binding.BindingDigest}"
        }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var actualRefs = policy.CandidateSet.SourceRefs.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (!policy.CandidateSet.Locked ||
            !string.Equals(policy.CandidateSet.SourceKind, ScreeningSourceKinds.LockedReviewableCandidateSet, StringComparison.Ordinal) ||
            !string.Equals(policy.CandidateSet.CreatedFromDedupResultId, binding.SourceResultId, StringComparison.Ordinal) ||
            !string.Equals(policy.CandidateSet.CreatedFromDedupResultDigest, binding.SourceResultDigest.ToString(), StringComparison.Ordinal) ||
            policy.CandidateSet.UnresolvedCandidates.Count != 0 ||
            !actualIds.SequenceEqual(expectedIds) ||
            !actualRefs.SequenceEqual(expectedRefs))
            throw Invalid(
                ScreeningCorpusBindingErrorCodes.MembershipMismatch,
                "Screening conduct policy does not bind the exact verified corpus snapshot units.");
        return new VerifiedSnapshotBoundScreeningPolicy(binding, policy);
    }

    private static VerifiedScreeningCorpusBinding BuildVerified(
        string bindingId,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        string snapshotId,
        ContentDigest snapshotRecordDigest,
        ContentDigest decisionSetDigest,
        IReadOnlyList<ScreeningCorpusGroupUnit> groupUnits,
        IReadOnlyList<ScreeningCorpusUnresolvedUnit> unresolvedUnits)
    {
        var content = BuildContent(
            bindingId, sourceResultId, sourceResultDigest, snapshotId, snapshotRecordDigest,
            decisionSetDigest, groupUnits, unresolvedUnits);
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            ScreeningCorpusBindingConstants.SchemaId,
            ScreeningCorpusBindingConstants.SchemaVersion,
            content);
        return new VerifiedScreeningCorpusBinding(
            bindingId, sourceResultId, sourceResultDigest, snapshotId, snapshotRecordDigest,
            decisionSetDigest, groupUnits, unresolvedUnits, envelope);
    }

    private static CanonicalJsonObject BuildContent(
        string bindingId,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        string snapshotId,
        ContentDigest snapshotRecordDigest,
        ContentDigest decisionSetDigest,
        IEnumerable<ScreeningCorpusGroupUnit> groupUnits,
        IEnumerable<ScreeningCorpusUnresolvedUnit> unresolvedUnits) =>
        new CanonicalJsonObject()
            .Add("binding_id", RequireText(bindingId, nameof(bindingId)))
            .Add("source_result_id", RequireText(sourceResultId, nameof(sourceResultId)))
            .Add("source_result_digest", sourceResultDigest.ToString())
            .Add("snapshot_id", RequireText(snapshotId, nameof(snapshotId)))
            .Add("snapshot_record_digest", snapshotRecordDigest.ToString())
            .Add("decision_set_digest", decisionSetDigest.ToString())
            .Add("group_units", CanonicalJsonValue.Array(groupUnits.Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                .Add("group_id", RequireText(item.GroupId, nameof(item.GroupId)))
                .Add("screening_candidate_id", RequireText(item.ScreeningCandidateId, nameof(item.ScreeningCandidateId)))
                .Add("member_candidate_ids", CanonicalJsonValue.Array(item.MemberCandidateIds.Select(CanonicalJsonValue.From).ToArray())))
                .ToArray()))
            .Add("unresolved_units", CanonicalJsonValue.Array(unresolvedUnits.Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                .Add("screening_candidate_id", RequireText(item.ScreeningCandidateId, nameof(item.ScreeningCandidateId)))
                .Add("candidate_digest", item.CandidateDigest.ToString()))
                .ToArray()));

    private static IReadOnlyDictionary<string, DedupCandidateRecord> BuildSourceIndex(
        VerifiedDeduplicationAuthorityResultDigest sourceResult)
    {
        var index = new Dictionary<string, DedupCandidateRecord>(StringComparer.Ordinal);
        foreach (var candidate in sourceResult.Result.RawCandidates)
        {
            var candidateId = RequireText(candidate.CandidateId, nameof(candidate.CandidateId));
            if (!index.TryAdd(candidateId, candidate))
                throw Invalid(ScreeningCorpusBindingErrorCodes.DuplicateUnit, "Source authority contains duplicate candidate ids.");
        }
        return index;
    }

    private static void EnsureSourceBinding(
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedCorpusSnapshot snapshot)
    {
        if (!string.Equals(snapshot.SourceResultId, sourceResult.Result.ResultId, StringComparison.Ordinal) ||
            snapshot.SourceResultDigest != sourceResult.ResultDigest)
            throw Invalid(ScreeningCorpusBindingErrorCodes.StaleSourceBinding, "Snapshot does not bind the verified Deduplication result.");
    }

    private static void AddExactMember(
        string candidateId,
        IReadOnlyDictionary<string, DedupCandidateRecord> sourceById,
        ISet<string> seen)
    {
        if (!sourceById.ContainsKey(candidateId))
            throw Invalid(ScreeningCorpusBindingErrorCodes.MembershipMismatch, "Snapshot references a candidate absent from source authority.");
        if (!seen.Add(candidateId))
            throw Invalid(ScreeningCorpusBindingErrorCodes.DuplicateUnit, "Snapshot candidate appears in more than one Screening unit.");
    }

    private static string RequireText(string value, string name)
    {
        try { return Guard.NotBlank(value, name); }
        catch (ArgumentException exception) { throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, exception.Message); }
    }

    private static ScreeningCorpusBindingException Invalid(string category, string message) => new(category, message);
}

public static class ScreeningCorpusBindingCanonicalCodec
{
    public static byte[] Serialize(VerifiedScreeningCorpusBinding binding) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes(
            (binding ?? throw new ArgumentNullException(nameof(binding))).ToCanonicalJson());

    public static VerifiedScreeningCorpusBinding Rehydrate(
        byte[] bytes,
        ContentDigest expectedDigest,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedCorpusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var parsed = CanonicalJsonValue.FromJsonElement(document.RootElement);
            if (parsed is not CanonicalJsonObject root || !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
                throw Invalid(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, "Canonical Screening corpus binding bytes are required.");
            var content = DigestEnvelope.RehydrateAndVerify(
                document.RootElement,
                expectedDigest,
                DigestScope.CanonicalJsonRecord,
                ScreeningCorpusBindingConstants.SchemaId,
                ScreeningCorpusBindingConstants.SchemaVersion).Envelope.Content;
            RequireExact(content,
            [
                "binding_id", "decision_set_digest", "group_units", "snapshot_id", "snapshot_record_digest",
                "source_result_digest", "source_result_id", "unresolved_units"
            ]);
            var input = new UnverifiedScreeningCorpusBinding(
                ScreeningCorpusBindingConstants.SchemaId,
                ScreeningCorpusBindingConstants.SchemaVersion,
                Text(content, "binding_id"),
                Text(content, "source_result_id"),
                Digest(content, "source_result_digest"),
                Text(content, "snapshot_id"),
                Digest(content, "snapshot_record_digest"),
                Digest(content, "decision_set_digest"),
                Array(content, "group_units").Select(ParseGroup).ToArray(),
                Array(content, "unresolved_units").Select(ParseUnresolved).ToArray(),
                expectedDigest);
            var verified = ScreeningCorpusBindingAuthority.Rehydrate(input, sourceResult, snapshot);
            if (!bytes.SequenceEqual(Serialize(verified)))
                throw Invalid(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, "Screening corpus binding bytes did not reproduce exactly.");
            return verified;
        }
        catch (ScreeningCorpusBindingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, $"Screening corpus binding verification failed: {exception.Message}");
        }
    }

    private static ScreeningCorpusGroupUnit ParseGroup(CanonicalJsonValue value)
    {
        var item = Object(value);
        RequireExact(item, ["group_id", "member_candidate_ids", "screening_candidate_id"]);
        return new ScreeningCorpusGroupUnit(
            Text(item, "group_id"),
            Text(item, "screening_candidate_id"),
            Array(item, "member_candidate_ids").Select(Text).ToArray());
    }

    private static ScreeningCorpusUnresolvedUnit ParseUnresolved(CanonicalJsonValue value)
    {
        var item = Object(value);
        RequireExact(item, ["candidate_digest", "screening_candidate_id"]);
        return new ScreeningCorpusUnresolvedUnit(
            Text(item, "screening_candidate_id"),
            Digest(item, "candidate_digest"));
    }

    private static void RequireExact(CanonicalJsonObject value, IEnumerable<string> required)
    {
        var expected = required.ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(value.Properties.Keys))
            throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, "Screening corpus binding has missing or unknown fields.");
    }

    private static CanonicalJsonObject Object(CanonicalJsonValue value) => value as CanonicalJsonObject
        ?? throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, "Screening corpus binding field must be an object.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonArray array
        ? array.Items : throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, $"Screening corpus binding field '{name}' must be an array.");
    private static string Text(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonString text
        ? text.Value : throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, $"Screening corpus binding field '{name}' must be text.");
    private static string Text(CanonicalJsonValue value) => value is CanonicalJsonString text
        ? text.Value : throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, "Screening corpus binding array entry must be text.");
    private static ContentDigest Digest(CanonicalJsonObject root, string name) => ContentDigest.Parse(Text(root, name));
    private static CanonicalJsonValue Value(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value)
        ? value : throw Invalid(ScreeningCorpusBindingErrorCodes.InvalidBinding, $"Screening corpus binding field '{name}' is required.");
    private static ScreeningCorpusBindingException Invalid(string category, string message) => new(category, message);
}
