namespace NexusScholar.ResearchWorkspace;

public sealed class ResearchWorkspaceMissingInputException : Exception
{
    public ResearchWorkspaceMissingInputException(string message)
        : base(message)
    {
    }
}

public sealed class ResearchWorkspaceDigestMismatchException : Exception
{
    public ResearchWorkspaceDigestMismatchException(string message)
        : base(message)
    {
    }
}
