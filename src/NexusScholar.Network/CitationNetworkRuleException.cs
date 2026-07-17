using NexusScholar.Kernel;

namespace NexusScholar.Network;

public sealed class CitationNetworkRuleException : DomainRuleException
{
    public CitationNetworkRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}
