using System.Text.Json.Serialization;

namespace NexusScholar.Cli.ResearchWorkspace;

internal sealed record ResearchWorkspaceInput
{
    public string? InputId { get; init; }

    public string? Id { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public string? RelativePath { get; init; }

    public string? Path { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public string? QueryId { get; init; }

    public string? QueryText { get; init; }

    public string? ImportTracePath { get; init; }

    [JsonIgnore]
    public string EffectiveInputId => InputId ?? Id ?? string.Empty;

    [JsonIgnore]
    public string EffectiveRelativePath => RelativePath ?? Path ?? string.Empty;
}
