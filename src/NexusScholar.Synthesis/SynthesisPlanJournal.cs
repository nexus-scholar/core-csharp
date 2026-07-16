namespace NexusScholar.Synthesis;

public sealed class SynthesisPlanJournal
{
    private readonly List<VerifiedSynthesisPlan> _plans = [];
    private readonly List<SynthesisInvalidation> _invalidations = [];
    public IReadOnlyList<VerifiedSynthesisPlan> Plans => _plans.AsReadOnly();
    public IReadOnlyList<SynthesisInvalidation> Invalidations => _invalidations.AsReadOnly();
    public IReadOnlyList<VerifiedSynthesisPlan> CurrentPlans
    {
        get
        {
            var invalidated = _invalidations.SelectMany(item => item.TargetDigests).ToHashSet();
            return _plans.Where(item => !invalidated.Contains(item.Digest)).ToArray();
        }
    }

    public void Append(VerifiedSynthesisPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (_plans.Any(item => item.Digest == plan.Digest)) throw Rule("Synthesis plan digest must be unique.");
        _plans.Add(plan);
    }

    public void Append(SynthesisInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        if (_invalidations.Any(item => item.Digest == invalidation.Digest) ||
            invalidation.TargetDigests.Any(digest => CurrentPlans.All(item => item.Digest != digest)))
            throw Rule("Synthesis invalidation must be unique and target current plans.");
        _invalidations.Add(invalidation);
    }

    private static SynthesisRuleException Rule(string message) => new(SynthesisErrorCodes.InvalidAuthority, message);
}
