namespace NexusScholar.Cli.ResearchWorkspace;

internal sealed record ResearchWorkspaceInput(
    string Id,
    string Kind,
    string Source,
    string Format,
    string Path,
    string Sha256);
