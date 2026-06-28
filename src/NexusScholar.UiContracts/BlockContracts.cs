using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusScholar.UiContracts;

public sealed record WorkspacePlan
{
    public WorkspacePlan(
        string workspaceId,
        string title,
        BlockMode mode,
        IReadOnlyList<ResearchBlockDescriptor> blocks,
        string? description = null,
        IReadOnlyList<EvidenceRef>? contextRefs = null)
    {
        WorkspaceId = UiContractGuard.NotBlank(workspaceId, nameof(workspaceId));
        Title = UiContractGuard.NotBlank(title, nameof(title));
        Mode = mode;
        Blocks = UiContractGuard.FreezeRequired(blocks, nameof(blocks));
        Description = UiContractGuard.OptionalTrim(description);
        ContextRefs = UiContractGuard.FreezeOptional(contextRefs);
    }

    public string WorkspaceId { get; }
    public string Title { get; }
    public BlockMode Mode { get; }
    public IReadOnlyList<ResearchBlockDescriptor> Blocks { get; }
    public string? Description { get; }
    public IReadOnlyList<EvidenceRef> ContextRefs { get; }
}

public sealed record ResearchBlockDescriptor
{
    public ResearchBlockDescriptor(
        string blockId,
        string kind,
        string title,
        BlockMode mode,
        BlockSeverity severity,
        BlockSourceKind sourceKind,
        IReadOnlyList<EvidenceRef> evidenceRefs,
        IReadOnlyList<ValidationRef> validationRefs,
        IReadOnlyList<BlockActionDescriptor> actions,
        string? summary = null,
        string? payloadJson = null)
    {
        BlockId = UiContractGuard.NotBlank(blockId, nameof(blockId));
        Kind = UiContractGuard.NotBlank(kind, nameof(kind));
        Title = UiContractGuard.NotBlank(title, nameof(title));
        Mode = mode;
        Severity = severity;
        SourceKind = sourceKind;
        EvidenceRefs = UiContractGuard.FreezeRequired(evidenceRefs, nameof(evidenceRefs));
        ValidationRefs = UiContractGuard.FreezeRequired(validationRefs, nameof(validationRefs));
        Actions = UiContractGuard.FreezeRequired(actions, nameof(actions));
        Summary = UiContractGuard.OptionalTrim(summary);
        PayloadJson = UiContractGuard.ValidObjectPayloadJson(payloadJson, nameof(payloadJson));
    }

    public string BlockId { get; }
    public string Kind { get; }
    public string Title { get; }
    public BlockMode Mode { get; }
    public BlockSeverity Severity { get; }
    public BlockSourceKind SourceKind { get; }
    public IReadOnlyList<EvidenceRef> EvidenceRefs { get; }
    public IReadOnlyList<ValidationRef> ValidationRefs { get; }
    public IReadOnlyList<BlockActionDescriptor> Actions { get; }
    public string? Summary { get; }
    public string? PayloadJson { get; }
}

public sealed record EvidenceRef
{
    public EvidenceRef(string kind, string value, string? label = null, string? digest = null, string? scope = null)
    {
        Kind = UiContractGuard.NotBlank(kind, nameof(kind));
        Value = UiContractGuard.NotBlank(value, nameof(value));
        Label = UiContractGuard.OptionalTrim(label);
        Digest = UiContractGuard.OptionalTrim(digest);
        Scope = UiContractGuard.OptionalTrim(scope);
    }

    public string Kind { get; }
    public string Value { get; }
    public string? Label { get; }
    public string? Digest { get; }
    public string? Scope { get; }
}

public sealed record ValidationRef
{
    public ValidationRef(string code, BlockSeverity severity, string? message = null, string? target = null)
    {
        Code = UiContractGuard.NotBlank(code, nameof(code));
        Severity = severity;
        Message = UiContractGuard.OptionalTrim(message);
        Target = UiContractGuard.OptionalTrim(target);
    }

    public string Code { get; }
    public BlockSeverity Severity { get; }
    public string? Message { get; }
    public string? Target { get; }
}

public sealed record BlockActionDescriptor
{
    public BlockActionDescriptor(
        string actionId,
        BlockActionKind kind,
        string label,
        bool requiresHumanConfirmation,
        bool isDestructive,
        string? commandKind = null,
        string? targetRef = null)
    {
        ActionId = UiContractGuard.NotBlank(actionId, nameof(actionId));
        Kind = kind;
        Label = UiContractGuard.NotBlank(label, nameof(label));
        RequiresHumanConfirmation = requiresHumanConfirmation;
        IsDestructive = isDestructive;
        CommandKind = UiContractGuard.OptionalTrim(commandKind);
        TargetRef = UiContractGuard.OptionalTrim(targetRef);
    }

    public string ActionId { get; }
    public BlockActionKind Kind { get; }
    public string Label { get; }
    public bool RequiresHumanConfirmation { get; }
    public bool IsDestructive { get; }
    public string? CommandKind { get; }
    public string? TargetRef { get; }
}

[JsonConverter(typeof(JsonStringEnumConverter<BlockMode>))]
public enum BlockMode
{
    Beginner,
    Audit,
    Review,
    Repair
}

[JsonConverter(typeof(JsonStringEnumConverter<BlockSeverity>))]
public enum BlockSeverity
{
    Info,
    Success,
    Warning,
    ReviewRequired,
    Blocking,
    Critical
}

[JsonConverter(typeof(JsonStringEnumConverter<BlockSourceKind>))]
public enum BlockSourceKind
{
    CoreValidated,
    ValidationReport,
    WorkflowState,
    AIProposal,
    UserDraft,
    AppProjection,
    ExternalEvidence,
    Sample
}

[JsonConverter(typeof(JsonStringEnumConverter<BlockActionKind>))]
public enum BlockActionKind
{
    OpenEvidence,
    ShowDetails,
    AcceptProposal,
    RejectProposal,
    EditDraft,
    RunValidation,
    AcceptMerge,
    RejectMerge,
    MarkUnresolved,
    ContinueWorkflow,
    ExportBundle
}

public static class KnownBlockKinds
{
    public const string ImportSummary = "nexus.block.import.summary";
    public const string ImportWarningSummary = "nexus.block.import.warning-summary";
    public const string DedupCandidateCluster = "nexus.block.dedup.candidate-cluster";
    public const string DedupRecordComparison = "nexus.block.dedup.record-comparison";
    public const string AIProposal = "nexus.block.ai.proposal";
    public const string HumanGateMergeDecision = "nexus.block.human-gate.merge-decision";
}

public static class KnownBlockActionKinds
{
    public const string OpenEvidence = nameof(BlockActionKind.OpenEvidence);
    public const string ShowDetails = nameof(BlockActionKind.ShowDetails);
    public const string AcceptProposal = nameof(BlockActionKind.AcceptProposal);
    public const string RejectProposal = nameof(BlockActionKind.RejectProposal);
    public const string EditDraft = nameof(BlockActionKind.EditDraft);
    public const string RunValidation = nameof(BlockActionKind.RunValidation);
    public const string AcceptMerge = nameof(BlockActionKind.AcceptMerge);
    public const string RejectMerge = nameof(BlockActionKind.RejectMerge);
    public const string MarkUnresolved = nameof(BlockActionKind.MarkUnresolved);
    public const string ContinueWorkflow = nameof(BlockActionKind.ContinueWorkflow);
    public const string ExportBundle = nameof(BlockActionKind.ExportBundle);
}

public static class KnownBlockActionCommandKinds
{
    public const string OpenEvidence = "nexus.command.open-evidence";
    public const string ShowDetails = "nexus.command.show-details";
    public const string AcceptMerge = "nexus.command.dedup.accept-merge";
    public const string RejectMerge = "nexus.command.dedup.reject-merge";
    public const string MarkUnresolved = "nexus.command.dedup.mark-unresolved";
    public const string ContinueWorkflow = "nexus.command.workflow.continue";
    public const string ExportBundle = "nexus.command.bundle.export";
}

public static class KnownEvidenceRefKinds
{
    public const string CoreRecord = "core-record";
    public const string SearchTrace = "search-trace";
    public const string SearchSighting = "search-sighting";
    public const string ImportSource = "import-source";
    public const string ImportRecord = "import-record";
    public const string DeduplicationResult = "deduplication-result";
    public const string SourceFileDigest = "source-file-digest";
    public const string RawRecordDigest = "raw-record-digest";
    public const string ValidationReport = "validation-report";
    public const string Sample = "sample";
}

public static class UiContractJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

internal static class UiContractGuard
{
    public static string NotBlank(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be blank.", parameterName);
        }

        return value.Trim();
    }

    public static string? OptionalTrim(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static IReadOnlyList<T> FreezeRequired<T>(IReadOnlyList<T>? values, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Any(value => value is null))
        {
            throw new ArgumentException("List must not contain null values.", parameterName);
        }

        return new ReadOnlyCollection<T>(values.ToArray());
    }

    public static IReadOnlyList<T> FreezeOptional<T>(IReadOnlyList<T>? values)
        where T : class
    {
        if (values is null)
        {
            return Array.Empty<T>();
        }

        if (values.Any(value => value is null))
        {
            throw new ArgumentException("List must not contain null values.", nameof(values));
        }

        return new ReadOnlyCollection<T>(values.ToArray());
    }

    public static string? ValidObjectPayloadJson(string? payloadJson, string parameterName)
    {
        var trimmed = OptionalTrim(payloadJson);
        if (trimmed is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Payload JSON root must be an object.", parameterName);
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Payload JSON must be valid JSON.", parameterName, exception);
        }

        return trimmed;
    }
}
