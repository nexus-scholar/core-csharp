namespace NexusScholar.Kernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IIdGenerator
{
    Guid NewId();
}

public sealed class GuidV7IdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}

public readonly record struct EntityId<TTag>
    where TTag : class
{
    private readonly Guid _value;

    private EntityId(Guid value)
    {
        _value = value;
    }

    public bool IsValid => _value != Guid.Empty;

    public Guid Value => IsValid
        ? _value
        : throw new InvalidOperationException("Default entity identifiers are invalid.");

    public static EntityId<TTag> From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Entity identifiers must not be empty.", nameof(value));
        }

        return new EntityId<TTag>(value);
    }

    public static EntityId<TTag> New(IIdGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        var value = generator.NewId();
        if (value == Guid.Empty)
        {
            throw new InvalidOperationException("ID generators must not return Guid.Empty.");
        }

        return From(value);
    }

    public override string ToString() => Value.ToString("D");
}
