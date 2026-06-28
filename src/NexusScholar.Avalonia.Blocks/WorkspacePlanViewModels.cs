using System.Text.Json;
using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks;

public sealed record WorkspacePlanViewModel
{
    private WorkspacePlanViewModel(
        string workspaceId,
        string title,
        string mode,
        string? description,
        string authorityStatus,
        IReadOnlyList<EvidenceRefViewModel> contextRefs,
        IReadOnlyList<ResearchBlockViewModel> blocks)
    {
        WorkspaceId = workspaceId;
        Title = title;
        Mode = mode;
        Description = description;
        AuthorityStatus = authorityStatus;
        ContextRefs = contextRefs;
        Blocks = blocks;
    }

    public string WorkspaceId { get; }
    public string Title { get; }
    public string Mode { get; }
    public string? Description { get; }
    public string AuthorityStatus { get; }
    public IReadOnlyList<EvidenceRefViewModel> ContextRefs { get; }
    public IReadOnlyList<ResearchBlockViewModel> Blocks { get; }

    public static WorkspacePlanViewModel FromJson(string json, BlockActionCallback? actionCallback = null)
    {
        var plan = JsonSerializer.Deserialize<WorkspacePlan>(json, UiContractJson.SerializerOptions)
            ?? throw new ArgumentException("WorkspacePlan JSON did not deserialize.", nameof(json));

        return FromWorkspacePlan(plan, actionCallback);
    }

    public static WorkspacePlanViewModel FromWorkspacePlan(WorkspacePlan plan, BlockActionCallback? actionCallback = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var contextRefs = plan.ContextRefs.Select(EvidenceRefViewModel.FromContract).ToArray();
        var blocks = plan.Blocks
            .Select((block, index) => ResearchBlockViewModel.FromContract(plan.WorkspaceId, block, index, actionCallback))
            .ToArray();

        return new WorkspacePlanViewModel(
            plan.WorkspaceId,
            plan.Title,
            plan.Mode.ToString(),
            plan.Description,
            DetectAuthorityStatus(plan, blocks),
            contextRefs,
            blocks);
    }

    private static string DetectAuthorityStatus(WorkspacePlan plan, IReadOnlyList<ResearchBlockViewModel> blocks)
    {
        var description = plan.Description ?? string.Empty;
        if (description.Contains("not Core authority", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("not a scientific fixture", StringComparison.OrdinalIgnoreCase) ||
            plan.ContextRefs.Any(reference => string.Equals(reference.Kind, KnownEvidenceRefKinds.Sample, StringComparison.Ordinal)) ||
            blocks.Any(block => string.Equals(block.SourceKind, BlockSourceKind.Sample.ToString(), StringComparison.Ordinal)))
        {
            return "Sample plan: non-authoritative renderer input.";
        }

        return "Renderer input only: scientific authority is unchanged.";
    }
}

public sealed record ResearchBlockViewModel
{
    private ResearchBlockViewModel(
        int order,
        string blockId,
        string kind,
        string title,
        string mode,
        string severity,
        string sourceKind,
        string? summary,
        IReadOnlyList<EvidenceRefViewModel> evidenceRefs,
        IReadOnlyList<ValidationRefViewModel> validationRefs,
        IReadOnlyList<BlockActionViewModel> actions,
        PayloadJsonViewModel payload)
    {
        Order = order;
        BlockId = blockId;
        Kind = kind;
        Title = title;
        Mode = mode;
        Severity = severity;
        SourceKind = sourceKind;
        Summary = summary;
        EvidenceRefs = evidenceRefs;
        ValidationRefs = validationRefs;
        Actions = actions;
        Payload = payload;
    }

    public int Order { get; }
    public string BlockId { get; }
    public string Kind { get; }
    public string Title { get; }
    public string Mode { get; }
    public string Severity { get; }
    public string SourceKind { get; }
    public string? Summary { get; }
    public IReadOnlyList<EvidenceRefViewModel> EvidenceRefs { get; }
    public IReadOnlyList<ValidationRefViewModel> ValidationRefs { get; }
    public IReadOnlyList<BlockActionViewModel> Actions { get; }
    public PayloadJsonViewModel Payload { get; }

    public static ResearchBlockViewModel FromContract(
        string workspaceId,
        ResearchBlockDescriptor block,
        int zeroBasedOrder,
        BlockActionCallback? actionCallback)
    {
        ArgumentNullException.ThrowIfNull(block);

        return new ResearchBlockViewModel(
            zeroBasedOrder + 1,
            block.BlockId,
            block.Kind,
            block.Title,
            block.Mode.ToString(),
            block.Severity.ToString(),
            block.SourceKind.ToString(),
            block.Summary,
            block.EvidenceRefs.Select(EvidenceRefViewModel.FromContract).ToArray(),
            block.ValidationRefs.Select(ValidationRefViewModel.FromContract).ToArray(),
            block.Actions.Select(action => BlockActionViewModel.FromContract(workspaceId, block.BlockId, action, actionCallback)).ToArray(),
            new PayloadJsonViewModel(block.PayloadJson));
    }
}

public sealed record EvidenceRefViewModel(string Kind, string Value, string? Label, string? Digest, string? Scope)
{
    public string DisplayLabel => Label ?? Value;

    public static EvidenceRefViewModel FromContract(EvidenceRef evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new EvidenceRefViewModel(evidence.Kind, evidence.Value, evidence.Label, evidence.Digest, evidence.Scope);
    }
}

public sealed record ValidationRefViewModel(string Code, string Severity, string? Message, string? Target)
{
    public static ValidationRefViewModel FromContract(ValidationRef validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return new ValidationRefViewModel(validation.Code, validation.Severity.ToString(), validation.Message, validation.Target);
    }
}

public sealed record BlockActionViewModel
{
    private readonly BlockActionCallback? _actionCallback;
    private readonly BlockActionInvocation _invocation;

    private BlockActionViewModel(
        string actionId,
        string kind,
        string label,
        bool requiresHumanConfirmation,
        bool isDestructive,
        string? commandKind,
        string? targetRef,
        BlockActionInvocation invocation,
        BlockActionCallback? actionCallback)
    {
        ActionId = actionId;
        Kind = kind;
        Label = label;
        RequiresHumanConfirmation = requiresHumanConfirmation;
        IsDestructive = isDestructive;
        CommandKind = commandKind;
        TargetRef = targetRef;
        _invocation = invocation;
        _actionCallback = actionCallback;
    }

    public string ActionId { get; }
    public string Kind { get; }
    public string Label { get; }
    public bool RequiresHumanConfirmation { get; }
    public bool IsDestructive { get; }
    public string? CommandKind { get; }
    public string? TargetRef { get; }

    public void Invoke() => _actionCallback?.Invoke(_invocation);

    public static BlockActionViewModel FromContract(
        string workspaceId,
        string blockId,
        BlockActionDescriptor action,
        BlockActionCallback? actionCallback)
    {
        ArgumentNullException.ThrowIfNull(action);

        var invocation = new BlockActionInvocation(
            workspaceId,
            blockId,
            action.ActionId,
            action.Kind,
            action.CommandKind,
            action.TargetRef,
            action.RequiresHumanConfirmation,
            action.IsDestructive);

        return new BlockActionViewModel(
            action.ActionId,
            action.Kind.ToString(),
            action.Label,
            action.RequiresHumanConfirmation,
            action.IsDestructive,
            action.CommandKind,
            action.TargetRef,
            invocation,
            actionCallback);
    }
}

public sealed record PayloadJsonViewModel(string? Json)
{
    public bool HasPayload => Json is not null;
}
