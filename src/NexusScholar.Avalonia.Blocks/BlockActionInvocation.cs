using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks;

public delegate void BlockActionCallback(BlockActionInvocation invocation);

public sealed record BlockActionInvocation(
    string WorkspaceId,
    string BlockId,
    string ActionId,
    BlockActionKind Kind,
    string? CommandKind,
    string? TargetRef,
    bool RequiresHumanConfirmation,
    bool IsDestructive);
