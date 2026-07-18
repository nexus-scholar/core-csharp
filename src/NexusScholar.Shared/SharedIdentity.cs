using System.Collections.ObjectModel;
using NexusScholar.Kernel;

namespace NexusScholar.Shared;

public static class SharedIdentityErrorCodes
{
    public const string UnknownWorkIdNamespace = "unknown-workid-namespace";
    public const string InvalidWorkId = "invalid-workid";
    public const string BlankWorkIdValue = "blank-workid-value";
    public const string EmptyTitle = "empty-title";
    public const string MissingSourceContext = "missing-source-context";
    public const string NoStableIdentity = "no-stable-identity";
    public const string NoIdentifierOverlap = "no-identifier-overlap";
    public const string DuplicateStableIdentity = "duplicate-stable-identity";
}

public sealed class SharedIdentityRuleException : DomainRuleException
{
    public SharedIdentityRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public readonly record struct WorkIdNamespace
{
    private static readonly string[] NamespaceValues =
    [
        "doi",
        "arxiv",
        "openalex",
        "s2",
        "pubmed",
        "pmcid",
        "ieee",
        "doaj",
        "internal"
    ];

    private static readonly string[] PrimaryPrecedenceValues =
    [
        "doi",
        "openalex",
        "s2",
        "arxiv",
        "pmcid",
        "pubmed",
        "ieee",
        "doaj",
        "internal"
    ];

    private static readonly HashSet<string> ApprovedNamespaceSet = new(NamespaceValues, StringComparer.Ordinal);

    private static readonly Dictionary<string, int> PrecedenceByNamespace =
        PrimaryPrecedenceValues.Select((value, index) => new { value, index })
            .ToDictionary(item => item.value, item => item.index, StringComparer.Ordinal);

    private WorkIdNamespace(string value)
    {
        Value = value;
    }

    public static IReadOnlyList<string> ApprovedNamespaces { get; } =
        new ReadOnlyCollection<string>(NamespaceValues.ToArray());

    public string Value { get; }

    public static WorkIdNamespace From(string value)
    {
        var normalized = Guard.NotBlank(value, nameof(value)).ToLowerInvariant();
        if (!ApprovedNamespaceSet.Contains(normalized))
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.UnknownWorkIdNamespace,
                $"Work id namespace '{value}' is not approved.");
        }

        return new WorkIdNamespace(normalized);
    }

    public static int Precedence(WorkIdNamespace value)
    {
        return PrecedenceByNamespace[value.Value];
    }

    public override string ToString() => Value;
}

public readonly record struct WorkId
{
    private WorkId(WorkIdNamespace idNamespace, string value)
    {
        Namespace = idNamespace;
        Value = value;
    }

    public WorkIdNamespace Namespace { get; }

    public string Value { get; }

    public static WorkId From(string idNamespace, string value)
    {
        var parsedNamespace = WorkIdNamespace.From(idNamespace);
        var normalizedValue = NormalizeValue(parsedNamespace, value);
        if (HasDisallowedLeadingNamespace(normalizedValue))
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.InvalidWorkId,
                $"Work ids cannot begin with a namespace prefix '{normalizedValue}' after normalization.");
        }

        return new WorkId(parsedNamespace, normalizedValue);
    }

    public static WorkId Parse(string value)
    {
        value = Guard.NotBlank(value, nameof(value));
        var separatorIndex = value.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.InvalidWorkId,
                "Work ids must use the strict '<namespace>:<value>' form.");
        }

        var namespaceValue = value[..separatorIndex];
        var identifierValue = value[(separatorIndex + 1)..];
        if (HasDisallowedLeadingNamespace(identifierValue))
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.InvalidWorkId,
                $"Work ids cannot begin with a namespace prefix '{identifierValue}' after normalization.");
        }

        return From(namespaceValue, identifierValue);
    }

    public override string ToString() => $"{Namespace}:{Value}";

    private static string NormalizeValue(WorkIdNamespace idNamespace, string value)
    {
        value = Guard.NotBlank(value, nameof(value));
        if (string.Equals(idNamespace.Value, "doi", StringComparison.Ordinal))
        {
            value = StripPrefix(value, "https://doi.org/");
            value = StripPrefix(value, "http://dx.doi.org/");
            value = StripPrefix(value, "doi:");
        }
        else if (string.Equals(idNamespace.Value, "arxiv", StringComparison.Ordinal))
        {
            value = StripPrefix(value, "arxiv:");
        }

        value = value.Trim().ToLowerInvariant();
        if (value.Length == 0)
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.BlankWorkIdValue,
                "Work id values must not normalize to blank.");
        }

        return value;
    }

    private static string StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : value;
    }

    private static bool HasDisallowedLeadingNamespace(string value)
    {
        var normalizedValue = value.Trim();
        foreach (var candidate in WorkIdNamespace.ApprovedNamespaces)
        {
            if (normalizedValue.StartsWith($"{candidate}:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class WorkIdSet
{
    private WorkIdSet(IEnumerable<WorkId> ids)
    {
        Ids = new ReadOnlyCollection<WorkId>(Normalize(ids));
    }

    public static WorkIdSet Empty { get; } = new(Array.Empty<WorkId>());

    public IReadOnlyList<WorkId> Ids { get; }

    public WorkId? Primary => Ids.Count == 0 ? null : Ids[0];

    public static WorkIdSet From(params WorkId[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return new WorkIdSet(ids);
    }

    public WorkIdSet Add(WorkId id) => new(Ids.Append(id));

    public WorkIdSet Merge(WorkIdSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new WorkIdSet(Ids.Concat(other.Ids));
    }

    public bool Contains(WorkId id) => Ids.Contains(id);

    public bool HasOverlapWith(WorkIdSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var existing = Ids.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        return other.Ids.Any(id => existing.Contains(id.ToString()));
    }

    private static WorkId[] Normalize(IEnumerable<WorkId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids
            .GroupBy(id => id.ToString(), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(id => WorkIdNamespace.Precedence(id.Namespace))
            .ThenBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class ScholarlyWork
{
    private ScholarlyWork(
        string title,
        WorkIdSet workIds,
        string? sourceContext,
        bool isRetracted,
        IReadOnlyDictionary<string, string>? rawData)
    {
        Title = NormalizeTitle(title);
        WorkIds = workIds ?? throw new ArgumentNullException(nameof(workIds));
        SourceContext = string.IsNullOrWhiteSpace(sourceContext) ? null : sourceContext.Trim();
        IsRetracted = isRetracted;
        RawData = Snapshot(rawData);
    }

    public string Title { get; }

    public WorkIdSet WorkIds { get; }

    public string? SourceContext { get; }

    public bool IsRetracted { get; }

    public IReadOnlyDictionary<string, string> RawData { get; }

    public WorkId? PrimaryWorkId => WorkIds.Primary;

    public bool HasStableIdentifier => WorkIds.Ids.Count > 0;

    public bool IsUnresolvedCandidate => !HasStableIdentifier;

    public static ScholarlyWork Identified(
        string title,
        WorkIdSet workIds,
        string? sourceContext = null,
        bool isRetracted = false,
        IReadOnlyDictionary<string, string>? rawData = null)
    {
        ArgumentNullException.ThrowIfNull(workIds);
        if (workIds.Ids.Count == 0)
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.NoStableIdentity,
                "Identified scholarly works require at least one stable identifier.");
        }

        return new ScholarlyWork(title, workIds, sourceContext, isRetracted, rawData);
    }

    public static ScholarlyWork UnresolvedCandidate(
        string title,
        string sourceContext,
        bool isRetracted = false,
        IReadOnlyDictionary<string, string>? rawData = null)
    {
        if (string.IsNullOrWhiteSpace(sourceContext))
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.MissingSourceContext,
                "No-id work candidates require source context.");
        }

        return new ScholarlyWork(title, WorkIdSet.Empty, sourceContext, isRetracted, rawData);
    }

    public static ScholarlyWork Reconstitute(
        string title,
        WorkIdSet workIds,
        string? sourceContext = null,
        bool isRetracted = false,
        IReadOnlyDictionary<string, string>? rawData = null)
    {
        ArgumentNullException.ThrowIfNull(workIds);
        return workIds.Ids.Count == 0
            ? UnresolvedCandidate(title, sourceContext ?? string.Empty, isRetracted, rawData)
            : Identified(title, workIds, sourceContext, isRetracted, rawData);
    }

    public bool IsSameWorkAs(ScholarlyWork other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return HasStableIdentifier && other.HasStableIdentifier && WorkIds.HasOverlapWith(other.WorkIds);
    }

    public ScholarlyWork MergeWith(ScholarlyWork other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!IsSameWorkAs(other))
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.NoIdentifierOverlap,
                "Scholarly works can merge only when stable identifiers overlap.");
        }

        return new ScholarlyWork(
            Title,
            WorkIds.Merge(other.WorkIds),
            SourceContext ?? other.SourceContext,
            IsRetracted || other.IsRetracted,
            RawData.Count > 0 ? RawData : other.RawData);
    }

    public ScholarlyWork WithRawData(IReadOnlyDictionary<string, string> rawData)
    {
        return new ScholarlyWork(Title, WorkIds, SourceContext, IsRetracted, rawData);
    }

    public ScholarlyWork WithoutRawData()
    {
        return new ScholarlyWork(Title, WorkIds, SourceContext, IsRetracted, null);
    }

    private static IReadOnlyDictionary<string, string> Snapshot(IReadOnlyDictionary<string, string>? rawData)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rawData is not null)
        {
            foreach (var pair in rawData)
            {
                copy.Add(pair.Key, pair.Value);
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new SharedIdentityRuleException(
                SharedIdentityErrorCodes.EmptyTitle,
                "Scholarly work titles must not be blank.");
        }

        return title.Trim();
    }
}

public sealed class CorpusSlice
{
    private CorpusSlice(IEnumerable<ScholarlyWork> works)
    {
        Works = new ReadOnlyCollection<ScholarlyWork>((works ?? throw new ArgumentNullException(nameof(works))).ToArray());
    }

    public static CorpusSlice Empty { get; } = new(Array.Empty<ScholarlyWork>());

    public IReadOnlyList<ScholarlyWork> Works { get; }

    public static CorpusSlice FromUnvalidatedCandidates(IEnumerable<ScholarlyWork> works)
    {
        return new CorpusSlice(works);
    }

    public static CorpusSlice RehydrateValidated(IEnumerable<ScholarlyWork> works)
    {
        var snapshot = (works ?? throw new ArgumentNullException(nameof(works))).ToArray();
        if (snapshot.Any(work => work is null))
        {
            throw new ArgumentException("Corpus members must not be null.", nameof(works));
        }

        var stableIds = new HashSet<WorkId>();
        foreach (var work in snapshot)
        {
            foreach (var id in work.WorkIds.Ids)
            {
                if (!stableIds.Add(id))
                {
                    throw new SharedIdentityRuleException(
                        SharedIdentityErrorCodes.DuplicateStableIdentity,
                        "Validated corpus members must not share a stable identifier.");
                }
            }
        }

        return new CorpusSlice(snapshot);
    }

    public CorpusSlice WithWork(ScholarlyWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var next = Works.ToArray();

        if (work.HasStableIdentifier)
        {
            var mergedIdentity = work;
            var overlappingIndexes = new HashSet<int>();
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var index = 0; index < next.Length; index++)
                {
                    if (overlappingIndexes.Contains(index) || !next[index].IsSameWorkAs(mergedIdentity))
                    {
                        continue;
                    }

                    overlappingIndexes.Add(index);
                    mergedIdentity = mergedIdentity.MergeWith(next[index]);
                    changed = true;
                }
            }

            if (overlappingIndexes.Count > 0)
            {
                var orderedIndexes = overlappingIndexes.OrderBy(index => index).ToArray();
                var merged = next[orderedIndexes[0]].MergeWith(work);
                foreach (var index in orderedIndexes.Skip(1))
                {
                    merged = merged.MergeWith(next[index]);
                }

                var result = next
                    .Where((_, index) => !overlappingIndexes.Contains(index))
                    .ToList();
                result.Insert(orderedIndexes[0], merged);
                return RehydrateValidated(result);
            }
        }

        return RehydrateValidated(next.Append(work));
    }

    public CorpusSlice Merge(CorpusSlice other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var result = this;
        foreach (var work in other.Works)
        {
            result = result.WithWork(work);
        }

        return result;
    }

    public ScholarlyWork? FindById(WorkId id)
    {
        return Works.FirstOrDefault(work => work.WorkIds.Contains(id));
    }

    public ScholarlyWork? FindByTitle(string title)
    {
        title = Guard.NotBlank(title, nameof(title));
        return Works.FirstOrDefault(work => string.Equals(work.Title, title, StringComparison.OrdinalIgnoreCase));
    }

    public CorpusSlice WithoutRetracted()
    {
        return new CorpusSlice(Works.Where(work => !work.IsRetracted));
    }

    public IReadOnlyList<string> StableMembershipIds()
    {
        var ids = new List<string>();
        foreach (var work in Works)
        {
            if (work.PrimaryWorkId is null)
            {
                throw new SharedIdentityRuleException(
                    SharedIdentityErrorCodes.NoStableIdentity,
                    "No-id unresolved candidates cannot satisfy immutable scientific identity membership.");
            }

            ids.Add(work.PrimaryWorkId.Value.ToString());
        }

        return new ReadOnlyCollection<string>(ids);
    }
}
