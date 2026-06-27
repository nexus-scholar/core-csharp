using NexusScholar.Kernel;

namespace NexusScholar.Screening;

public sealed class ScreeningRuleException : DomainRuleException
{
    public ScreeningRuleException(string category, string message)
        : base(message)
    {
        Category = category;
    }

    public string Category { get; }
}
