using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace NexusScholar.Kernel;

public enum CanonicalStringNormalizationMode
{
    NormalizeToNfc = 0,
    RequireNormalized = 1
}

public sealed record CanonicalJsonSerializerOptions
{
    public static CanonicalJsonSerializerOptions Default { get; } = new();

    public CanonicalStringNormalizationMode StringNormalization { get; init; } = CanonicalStringNormalizationMode.NormalizeToNfc;
}

public abstract record CanonicalJsonValue
{
    public static CanonicalJsonValue Null() => CanonicalJsonNull.Instance;

    public static CanonicalJsonValue From(bool value) => new CanonicalJsonBoolean(value);

    public static CanonicalJsonValue From(string value) => new CanonicalJsonString(value);

    public static CanonicalJsonValue From(int value) => CanonicalJsonNumber.From(value);

    public static CanonicalJsonValue From(long value) => CanonicalJsonNumber.From(value);

    public static CanonicalJsonValue From(decimal value) => CanonicalJsonNumber.From(value);

    public static CanonicalJsonValue From(double value) => CanonicalJsonNumber.From(value);

    public static CanonicalJsonValue From(float value) => CanonicalJsonNumber.From(value);

    public static CanonicalJsonArray Array(params CanonicalJsonValue[] items) => new(items);

    public static CanonicalJsonObject Object(params (string Name, CanonicalJsonValue Value)[] properties)
    {
        var result = new CanonicalJsonObject();
        foreach (var (name, value) in properties)
        {
            result.Add(name, value);
        }

        return result;
    }

    public static CanonicalJsonValue FromJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => Null(),
            JsonValueKind.False => From(false),
            JsonValueKind.True => From(true),
            JsonValueKind.String => From(element.GetString()!),
            JsonValueKind.Number => ParseNumber(element.GetRawText()),
            JsonValueKind.Array => new CanonicalJsonArray(element.EnumerateArray().Select(FromJsonElement)),
            JsonValueKind.Object => ParseObject(element),
            _ => throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.")
        };
    }

    public static CanonicalJsonValue DeepClone(CanonicalJsonValue value)
    {
        return value switch
        {
            CanonicalJsonNull => Null(),
            CanonicalJsonBoolean booleanValue => From(booleanValue.Value),
            CanonicalJsonString stringValue => From(stringValue.Value),
            CanonicalJsonNumber numberValue => CanonicalJsonNumber.FromCanonicalString(numberValue.Value),
            CanonicalJsonArray arrayValue => new CanonicalJsonArray(arrayValue.Items.Select(DeepClone)),
            CanonicalJsonObject objectValue => CloneObject(objectValue),
            _ => throw new InvalidOperationException($"Unsupported canonical JSON node type '{value.GetType().Name}'.")
        };
    }

    private static CanonicalJsonValue ParseNumber(string rawText)
    {
        if (!double.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            throw new InvalidOperationException($"JSON number '{rawText}' is not supported by the canonical parser.");
        }

        return From(doubleValue);
    }

    private static CanonicalJsonObject ParseObject(JsonElement element)
    {
        var result = new CanonicalJsonObject();
        foreach (var property in element.EnumerateObject())
        {
            result.Add(property.Name, FromJsonElement(property.Value));
        }

        return result;
    }

    private static CanonicalJsonObject CloneObject(CanonicalJsonObject value)
    {
        var clone = new CanonicalJsonObject();
        foreach (var property in value.Properties)
        {
            clone.Add(property.Key, DeepClone(property.Value));
        }

        return clone;
    }
}

internal sealed record CanonicalJsonNull : CanonicalJsonValue
{
    public static CanonicalJsonNull Instance { get; } = new();

    private CanonicalJsonNull()
    {
    }
}

public sealed record CanonicalJsonBoolean(bool Value) : CanonicalJsonValue;

public sealed record CanonicalJsonString : CanonicalJsonValue
{
    public CanonicalJsonString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public string Value { get; }
}

public sealed record CanonicalJsonNumber : CanonicalJsonValue
{
    private CanonicalJsonNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public new static CanonicalJsonNumber From(int value) => new(value.ToString(CultureInfo.InvariantCulture));

    public new static CanonicalJsonNumber From(decimal value)
    {
        var finiteDoubleValue = (double)value;
        EnsureFinite(finiteDoubleValue);

        decimal roundTrippedValue;
        try
        {
            roundTrippedValue = (decimal)finiteDoubleValue;
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                exception,
                "Decimal values that are not exactly representable as finite binary64 must be modeled as strings.");
        }

        if (value != roundTrippedValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Decimal values that are not exactly representable as finite binary64 must be modeled as strings.");
        }

        return new(CanonicalizeNumber(finiteDoubleValue));
    }

    public new static CanonicalJsonNumber From(long value)
    {
        var finiteDoubleValue = (double)value;
        EnsureFinite(finiteDoubleValue);

        if ((long)finiteDoubleValue != value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Long values that cannot be represented exactly as finite binary64 must be modeled as strings.");
        }

        return From(finiteDoubleValue);
    }

    public new static CanonicalJsonNumber From(double value)
    {
        EnsureFinite(value);
        return new(CanonicalizeNumber(value));
    }

    public new static CanonicalJsonNumber From(float value) => From((double)value);

    internal static CanonicalJsonNumber FromCanonicalString(string value) => new(value);

    private static void EnsureFinite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "NaN and infinities are forbidden in canonical JSON.");
        }
    }

    private static string CanonicalizeNumber(double value)
    {
        if (value == 0d)
        {
            return "0";
        }

        var text = value.ToString("R", CultureInfo.InvariantCulture);

        if (RequiresScientificNotation(value))
        {
            return CanonicalizeExponent(text);
        }

        if (text.IndexOfAny(['e', 'E']) < 0)
        {
            return text;
        }

        return CanonicalizeToDecimalFromScientific(text);
    }

    private static bool RequiresScientificNotation(double value)
    {
        var absoluteValue = Math.Abs(value);
        return absoluteValue < 1e-6 || absoluteValue >= 1e21;
    }

    private static string CanonicalizeExponent(string text)
    {
        var exponentIndex = text.IndexOfAny(['E', 'e']);
        if (exponentIndex < 0)
        {
            return text;
        }

        var mantissa = text[..exponentIndex];
        var exponent = int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);

        return exponent >= 0
            ? $"{mantissa}e+{exponent}"
            : $"{mantissa}e{exponent}";
    }

    private static string CanonicalizeToDecimalFromScientific(string text)
    {
        var exponentIndex = text.IndexOfAny(['E', 'e']);
        if (exponentIndex < 0)
        {
            return text;
        }

        var negative = text[0] == '-';
        var mantissa = negative ? text[1..exponentIndex] : text[..exponentIndex];
        var exponent = int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);

        var pointIndex = mantissa.IndexOf('.');
        var integerDigits = pointIndex < 0 ? mantissa : mantissa[..pointIndex];
        var fractionalDigits = pointIndex < 0 ? string.Empty : mantissa[(pointIndex + 1)..];
        var digits = integerDigits + fractionalDigits;
        var decimalPoint = integerDigits.Length + exponent;

        var normalized = decimalPoint <= 0
            ? $"0.{new string('0', -decimalPoint)}{digits}"
            : decimalPoint >= digits.Length
                ? digits + new string('0', decimalPoint - digits.Length)
                : digits.Insert(decimalPoint, ".");

        normalized = TrimTrailingDecimalZeros(normalized);
        return negative ? $"-{normalized}" : normalized;
    }

    private static string TrimTrailingDecimalZeros(string text)
    {
        if (!text.Contains('.'))
        {
            return text;
        }

        var trimmed = text.TrimEnd('0');
        if (trimmed.EndsWith('.'))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }
}

public sealed record CanonicalJsonArray : CanonicalJsonValue
{
    private readonly IReadOnlyList<CanonicalJsonValue> _items;

    public CanonicalJsonArray(IEnumerable<CanonicalJsonValue> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = new ReadOnlyCollection<CanonicalJsonValue>(items.ToArray());
    }

    public IReadOnlyList<CanonicalJsonValue> Items => _items;

    public void Freeze()
    {
        foreach (var item in Items)
        {
            if (item is CanonicalJsonObject objectValue)
            {
                objectValue.Freeze();
            }
            else if (item is CanonicalJsonArray arrayValue)
            {
                arrayValue.Freeze();
            }
        }
    }
}

public sealed record CanonicalJsonObject : CanonicalJsonValue
{
    private readonly Dictionary<string, CanonicalJsonValue> _properties = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, CanonicalJsonValue> _publicProperties;
    private bool _isReadOnly;

    public CanonicalJsonObject()
    {
        _publicProperties = new ReadOnlyDictionary<string, CanonicalJsonValue>(_properties);
    }

    public IReadOnlyDictionary<string, CanonicalJsonValue> Properties => _publicProperties;

    public CanonicalJsonObject Add(string name, CanonicalJsonValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(name);
        EnsureWritable();

        if (_properties.ContainsKey(name))
        {
            throw new InvalidOperationException($"Canonical JSON object already contains property '{name}'.");
        }

        _properties.Add(name, value);
        return this;
    }

    public CanonicalJsonObject Add(string name, string value) => Add(name, CanonicalJsonValue.From(value));

    public CanonicalJsonObject Add(string name, bool value) => Add(name, CanonicalJsonValue.From(value));

    public CanonicalJsonObject Add(string name, int value) => Add(name, CanonicalJsonValue.From(value));

    public CanonicalJsonObject Add(string name, long value) => Add(name, CanonicalJsonValue.From(value));

    public CanonicalJsonObject Add(string name, decimal value) => Add(name, CanonicalJsonValue.From(value));

    public CanonicalJsonObject Add(string name, double value) => Add(name, CanonicalJsonValue.From(value));

    public CanonicalJsonObject AddTimestamp(string name, DateTimeOffset value) => Add(name, CanonicalTimestamp.FormatUtc(value));

    public CanonicalJsonObject AddNull(string name) => Add(name, CanonicalJsonValue.Null());

    public bool Contains(string name) => _properties.ContainsKey(name);

    public CanonicalJsonObject Freeze()
    {
        foreach (var value in _properties.Values)
        {
            if (value is CanonicalJsonObject objectValue)
            {
                objectValue.Freeze();
            }
            else if (value is CanonicalJsonArray arrayValue)
            {
                arrayValue.Freeze();
            }
        }

        _isReadOnly = true;
        return this;
    }

    private void EnsureWritable()
    {
        if (_isReadOnly)
        {
            throw new InvalidOperationException("Canonical JSON objects are immutable once frozen.");
        }
    }
}

public static class CanonicalJsonSerializer
{
    public const string ProfileId = "nexus-jcs-nfc-v1";

    public static byte[] SerializeToUtf8Bytes(CanonicalJsonValue value, CanonicalJsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.UTF8.GetBytes(Serialize(value, options));
    }

    public static string Serialize(CanonicalJsonValue value, CanonicalJsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder();
        WriteValue(builder, value, options ?? CanonicalJsonSerializerOptions.Default);
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, CanonicalJsonValue value, CanonicalJsonSerializerOptions options)
    {
        switch (value)
        {
            case CanonicalJsonNull:
                builder.Append("null");
                break;

            case CanonicalJsonBoolean booleanValue:
                builder.Append(booleanValue.Value ? "true" : "false");
                break;

            case CanonicalJsonString stringValue:
                WriteJsonString(builder, NormalizeString(stringValue.Value, options));
                break;

            case CanonicalJsonNumber numberValue:
                builder.Append(numberValue.Value);
                break;

            case CanonicalJsonArray arrayValue:
                builder.Append('[');
                for (var index = 0; index < arrayValue.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    WriteValue(builder, arrayValue.Items[index], options);
                }

                builder.Append(']');
                break;

            case CanonicalJsonObject objectValue:
                builder.Append('{');
                var first = true;
                foreach (var property in NormalizeAndSortProperties(objectValue, options))
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    WriteJsonString(builder, property.Name);
                    builder.Append(':');
                    WriteValue(builder, property.Value, options);
                    first = false;
                }

                builder.Append('}');
                break;

            default:
                throw new InvalidOperationException($"Unsupported canonical JSON node type '{value.GetType().Name}'.");
        }
    }

    private static string NormalizeString(string value, CanonicalJsonSerializerOptions options)
    {
        if (options.StringNormalization == CanonicalStringNormalizationMode.RequireNormalized &&
            !value.IsNormalized(NormalizationForm.FormC))
        {
            throw new InvalidOperationException("Canonical JSON validation mode requires NFC-normalized string input.");
        }

        return value.IsNormalized(NormalizationForm.FormC)
            ? value
            : value.Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyList<CanonicalProperty> NormalizeAndSortProperties(
        CanonicalJsonObject objectValue,
        CanonicalJsonSerializerOptions options)
    {
        var properties = objectValue.Properties
            .Select(pair => new CanonicalProperty(NormalizeString(pair.Key, options), pair.Value))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        for (var index = 1; index < properties.Length; index++)
        {
            if (string.Equals(properties[index - 1].Name, properties[index].Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Canonical JSON normalization produced duplicate property names.");
            }
        }

        return properties;
    }

    private static void WriteJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;

                case '\\':
                    builder.Append("\\\\");
                    break;

                case '\b':
                    builder.Append("\\b");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                case '\f':
                    builder.Append("\\f");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                default:
                    if (character <= '\u001f')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        break;
                    }

                    if (char.IsHighSurrogate(character))
                    {
                        if (index == value.Length - 1 || !char.IsLowSurrogate(value[index + 1]))
                        {
                            throw new InvalidOperationException("Canonical JSON strings must not contain lone surrogate code units.");
                        }

                        builder.Append(character);
                        builder.Append(value[index + 1]);
                        index++;
                        break;
                    }

                    if (char.IsLowSurrogate(character))
                    {
                        throw new InvalidOperationException("Canonical JSON strings must not contain lone surrogate code units.");
                    }

                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
    }

    private sealed record CanonicalProperty(string Name, CanonicalJsonValue Value);
}
