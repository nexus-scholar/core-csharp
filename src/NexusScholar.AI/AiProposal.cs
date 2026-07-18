using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexusScholar.Kernel;

namespace NexusScholar.AI;

public sealed record AiProposal<T>
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.General)
    {
        IncludeFields = true
    };
    private readonly byte[] valueSnapshot;

    public AiProposal(
        AiTaskPolicy policy,
        T value,
        IReadOnlyList<ContentDigest> evidence,
        DateTimeOffset createdAt)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (value is null)
        {
            throw new DomainRuleException("AI proposal value must not be null.");
        }

        valueSnapshot = Snapshot(value);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Any(item => !item.IsValid))
        {
            throw new DomainRuleException("AI proposal evidence must use valid content digests.");
        }
        if (policy.EvidenceRequired && evidence.Count == 0)
        {
            throw new DomainRuleException("This AI task policy requires proposal evidence.");
        }
        if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("AI proposal creation time must be a non-default UTC timestamp.");
        }

        Evidence = new ReadOnlyCollection<ContentDigest>(evidence.ToArray());
        CreatedAt = createdAt;
    }

    public AiTaskPolicy Policy { get; }

    public string TaskType => Policy.TaskType;

    public T Value => RehydrateValue();

    public IReadOnlyList<ContentDigest> Evidence { get; }

    public DateTimeOffset CreatedAt { get; }

    private static byte[] Snapshot(T value)
    {
        var inspectedTypes = new HashSet<Type>();
        ValidateSnapshotType(typeof(T), inspectedTypes);
        ValidateSnapshotType(value!.GetType(), inspectedTypes);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SnapshotOptions);
            var rehydrated = Rehydrate(bytes);
            if (rehydrated!.GetType() != value.GetType())
            {
                throw new DomainRuleException(
                    "AI proposal value cannot be snapshotted without preserving its runtime type.");
            }

            if (!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(rehydrated, SnapshotOptions)))
            {
                throw new DomainRuleException("AI proposal value cannot be snapshotted without preserving all serializable state.");
            }

            return bytes;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new DomainRuleException($"AI proposal value cannot be snapshotted: {exception.Message}");
        }
    }

    private T RehydrateValue()
    {
        try
        {
            return Rehydrate(valueSnapshot);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new DomainRuleException($"AI proposal value snapshot cannot be rehydrated: {exception.Message}");
        }
    }

    private static T Rehydrate(byte[] bytes) =>
        JsonSerializer.Deserialize<T>(bytes, SnapshotOptions)
        ?? throw new DomainRuleException("AI proposal value snapshot cannot rehydrate to null.");

    private static void ValidateSnapshotType(Type type, ISet<Type> inspected)
    {
        if (!inspected.Add(type))
        {
            return;
        }

        if (type.IsArray)
        {
            ValidateSnapshotType(type.GetElementType()!, inspected);
            return;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            ValidateSnapshotType(argument, inspected);
        }

        if (type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type.Namespace is "System" ||
            type.Namespace?.StartsWith("System.", StringComparison.Ordinal) == true ||
            type.GetCustomAttribute<JsonConverterAttribute>() is not null)
        {
            return;
        }

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fields.Any(field =>
                field.GetCustomAttribute<JsonIgnoreAttribute>() is not null ||
                (!field.IsPublic &&
                 field.GetCustomAttribute<JsonIncludeAttribute>() is null &&
                 !HasSerializablePropertyRepresentation(type, field))))
        {
            throw new DomainRuleException(
                $"AI proposal value type '{type.FullName}' contains private state without an explicit JSON representation.");
        }

        foreach (var field in fields.Where(field => field.IsPublic || field.GetCustomAttribute<JsonIncludeAttribute>() is not null))
        {
            ValidateSnapshotType(field.FieldType, inspected);
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.GetMethod is not null))
        {
            ValidateSnapshotType(property.PropertyType, inspected);
        }
    }

    private static bool HasSerializablePropertyRepresentation(Type type, FieldInfo field)
    {
        if (field.GetCustomAttribute<CompilerGeneratedAttribute>() is null ||
            !field.Name.StartsWith('<') ||
            !field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal))
        {
            return false;
        }

        var propertyNameEnd = field.Name.IndexOf('>');
        if (propertyNameEnd <= 1)
        {
            return false;
        }

        var property = type.GetProperty(
            field.Name[1..propertyNameEnd],
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property is not null &&
            property.GetCustomAttribute<JsonIgnoreAttribute>() is null &&
            (property.GetMethod?.IsPublic == true ||
             property.GetCustomAttribute<JsonIncludeAttribute>() is not null);
    }
}
