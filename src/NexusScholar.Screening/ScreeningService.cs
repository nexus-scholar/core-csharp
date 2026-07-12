using System.Collections.ObjectModel;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Screening;

public sealed class ScreeningService
{
    private readonly ScreeningCandidateSet _candidateSet;
    private readonly Dictionary<string, ScreeningCriteria> _criteriaById;
    private readonly List<ScreeningDecision> _decisions = [];
    private readonly List<ScreeningSuggestion> _suggestions = [];
    private readonly Dictionary<string, ScreeningConflict> _conflicts = new(StringComparer.Ordinal);

    internal ScreeningService(
        ScreeningCandidateSet candidateSet,
        IEnumerable<ScreeningCriteria> criteria)
        : this(candidateSet, criteria, null)
    {
    }

    public ScreeningService(
        VerifiedProtocolVersion protocolAuthority,
        VerifiedDeduplicationResult deduplicationAuthority,
        string candidateSetId,
        IEnumerable<ScreeningCriteria> criteria)
        : this(
            ScreeningCandidateSet.CreateFromDedupResult(
                Guard.NotBlank(candidateSetId, nameof(candidateSetId)),
                (deduplicationAuthority ?? throw new ArgumentNullException(nameof(deduplicationAuthority))).Result,
                true,
                ScreeningSourceKinds.DeduplicationResult,
                new[] { deduplicationAuthority.Result.ResultId },
                null,
                deduplicationAuthority.Result.NonClaims),
            criteria,
            protocolAuthority ?? throw new ArgumentNullException(nameof(protocolAuthority)))
    {
    }

    private ScreeningService(
        ScreeningCandidateSet candidateSet,
        IEnumerable<ScreeningCriteria> criteria,
        VerifiedProtocolVersion? protocolAuthority)
    {
        _candidateSet = candidateSet ?? throw new ArgumentNullException(nameof(candidateSet));

        _criteriaById = new Dictionary<string, ScreeningCriteria>(StringComparer.Ordinal);
        if (criteria is not null)
        {
            foreach (var item in criteria)
            {
                _criteriaById[BuildCriteriaKey(item.CriteriaId, item.Stage)] = item;
                if (protocolAuthority is not null && item.RequiresProtocolBinding)
                {
                    var version = protocolAuthority.Version;
                    if (!string.Equals(item.ApprovedProtocolBinding, version.Id, StringComparison.Ordinal) ||
                        !string.Equals(item.ApprovedProtocolDigest, version.ContentDigest.ToString(), StringComparison.Ordinal) ||
                        !string.Equals(item.ApprovedProtocolDigestScope, DigestScope.ProtocolContent.ToString(), StringComparison.Ordinal) ||
                        !ScreeningProtocolBindingStatus.IsApproved(item.ApprovedProtocolStatus))
                    {
                        throw new ScreeningRuleException(ScreeningErrorCodes.InvalidProtocolBinding, "Screening criteria do not match verified Protocol authority.");
                    }
                }
            }
        }
    }

    public ScreeningCandidateSet CandidateSet => _candidateSet;

    public IReadOnlyList<ScreeningDecision> Decisions => new ReadOnlyCollection<ScreeningDecision>(_decisions);

    public IReadOnlyList<ScreeningSuggestion> Suggestions => new ReadOnlyCollection<ScreeningSuggestion>(_suggestions);

    public IReadOnlyList<ScreeningConflict> Conflicts => new ReadOnlyCollection<ScreeningConflict>(_conflicts.Values.ToArray());

    public static ScreeningCandidateSet CreateCandidateSetFromInput(
        object? screeningInput,
        string candidateSetId,
        bool locked,
        string sourceKind = ScreeningSourceKinds.DeduplicationResult)
    {
        if (screeningInput is null)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Screening input is required.");
        }

        if (string.Equals(screeningInput.GetType().Name, "SearchTrace", StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.RawSearchTraceNotScreenable,
                "Raw Search trace input is not accepted directly as Screening input.");
        }

        if (screeningInput is not DeduplicationResult dedupResult)
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.InvalidScreeningInput,
                "Screening input must be a deduplication result.");
        }

        return ScreeningCandidateSet.CreateFromDedupResult(
            Guard.NotBlank(candidateSetId, nameof(candidateSetId)),
            dedupResult,
            locked,
            Guard.NotBlank(sourceKind, nameof(sourceKind)),
            new[] { dedupResult.SchemaId },
            null,
            [ScreeningRuleConstantsNoPhpCompat]);
    }

    public static ScreeningCandidateSet CreateCandidateSetFromDedupResult(
        string candidateSetId,
        DeduplicationResult dedupResult,
        bool locked,
        string sourceKind = ScreeningSourceKinds.DeduplicationResult,
        IReadOnlyList<string>? sourceRefs = null,
        IReadOnlyList<string>? nonClaims = null)
    {
        return ScreeningCandidateSet.CreateFromDedupResult(
            Guard.NotBlank(candidateSetId, nameof(candidateSetId)),
            dedupResult ?? throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Deduplication result is required."),
            locked,
            Guard.NotBlank(sourceKind, nameof(sourceKind)),
            sourceRefs,
            null,
            nonClaims);
    }

    public void AddSuggestion(ScreeningSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        if (_suggestions.Any(existing => string.Equals(existing.SuggestionId, suggestion.SuggestionId, StringComparison.Ordinal)))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.DuplicateDecisionId, "Suggestion identifier already exists.");
        }

        if (!ScreeningStages.IsKnown(suggestion.Stage))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.UnknownScreeningStage, $"Unknown screening stage '{suggestion.Stage}'.");
        }

        if (!ScreeningVerdicts.IsKnown(suggestion.SuggestedVerdict))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.UnknownScreeningVerdict, $"Unknown screening verdict '{suggestion.SuggestedVerdict}'.");
        }

        EnsureCandidateExists(suggestion.CandidateSetId, suggestion.CandidateId);
        EnsureNoProjectionIdentity(
            suggestion.SuggestionId,
            null,
            suggestion.EvidenceRefs,
            sourceSuggestionIds: string.IsNullOrWhiteSpace(suggestion.SourceDecisionId)
                ? null
                : [suggestion.SourceDecisionId]);
        EnsureNoPathIdentity(suggestion.EvidenceRefs);
        EnsureConfidenceRange(suggestion.Confidence, includeBoundary: true);

        if (string.Equals(suggestion.Stage, ScreeningStages.FullText, StringComparison.Ordinal))
        {
            ThrowIfMissingRequiredFullTextEvidence(suggestion.EvidenceRefs, $"Suggestion {suggestion.SuggestionId}");
        }

        _suggestions.Add(suggestion);
    }

    public void AddDecision(ScreeningDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.DecisionKind == ScreeningDecisionKind.Suggestion)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.AutomationCannotFinalize, "Suggestion records cannot become final screening decisions.");
        }

        if (_decisions.Any(existing => string.Equals(existing.DecisionId, decision.DecisionId, StringComparison.Ordinal)))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.DuplicateDecisionId, "Decision identifier already exists.");
        }

        EnsureCandidateExists(decision.CandidateSetId, decision.CandidateId);

        if (!ScreeningStages.IsKnown(decision.Stage))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.UnknownScreeningStage, $"Unknown screening stage '{decision.Stage}'.");
        }

        if (!ScreeningVerdicts.IsKnown(decision.Verdict))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.UnknownScreeningVerdict, $"Unknown screening verdict '{decision.Verdict}'.");
        }

        if (decision.IsFinal && !_candidateSet.Locked)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.CandidateSetNotLocked, "Candidate set must be locked before final or adjudication decisions.");
        }

        if (decision.IsFinal && decision.Actor is null)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingHumanActor, "Final screening decisions require an actor.");
        }

        if (decision.IsFinal && decision.Actor is { IsHuman: false })
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.AutomationCannotFinalize, "Only human actors can submit final screening decisions.");
        }

        if (decision.IsFinal && string.IsNullOrWhiteSpace(decision.Rationale))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingRationale, "Final screening decisions require rationale.");
        }

        EnsureConfidenceRange(decision.Confidence, includeBoundary: true);
        EnsureNoProjectionIdentity(
            decision.DecisionId,
            decision.DecisionKind == ScreeningDecisionKind.Adjudication ? decision.ResolvedConflictId : decision.ConflictId,
            decision.EvidenceRefs,
            decision.SourceDecisionIds,
            decision.SourceSuggestionIds,
            decision.Stage);

        EnsureNoPathIdentity(decision.EvidenceRefs);
        EnsureFinalDecisionCriteria(decision);

        if (decision.IsFinal && string.Equals(decision.Stage, ScreeningStages.FullText, StringComparison.Ordinal))
        {
            ThrowIfMissingRequiredFullTextEvidence(decision.EvidenceRefs, $"Decision {decision.DecisionId}");
        }

        ThrowIfUnresolvedConflictBlocksDownstream(decision);

        if (decision.DecisionKind == ScreeningDecisionKind.Adjudication)
        {
            EnsureAdjudicationResolution(decision);
        }

        _decisions.Add(decision);

        if (decision.DecisionKind is ScreeningDecisionKind.Human)
        {
            UpsertConflictForKey(decision);
        }
    }

    private void EnsureCandidateExists(string candidateSetId, string candidateId)
    {
        if (!string.Equals(_candidateSet.CandidateSetId, candidateSetId, StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Decision candidate set id does not match session candidate set id.");
        }

        if (!_candidateSet.HasCandidate(candidateId))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.CandidateNotInSet, $"Candidate '{candidateId}' is not part of this candidate set.");
        }
    }

    private void EnsureConfidenceRange(double? confidence, bool includeBoundary)
    {
        if (confidence is null)
        {
            return;
        }

        if (!double.IsFinite(confidence.Value) || confidence < 0d || confidence > 1d)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConfidence, "Confidence must be within [0, 1] when present.");
        }

        if (!includeBoundary && (confidence <= 0d || confidence >= 1d))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConfidence, "Confidence must be strictly within (0, 1) when provided.");
        }
    }

    private void EnsureFinalDecisionCriteria(ScreeningDecision decision)
    {
        if (string.IsNullOrWhiteSpace(decision.CriteriaDigest))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingCriteriaDigest, "Decision criteria digest is required.");
        }

        if (_criteriaById.Count == 0)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "No criteria are configured for this Screening session.");
        }

        var key = BuildCriteriaKey(decision.CriteriaRef, decision.Stage);
        if (!_criteriaById.TryGetValue(key, out var criteria))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Decision criteria reference is unknown.");
        }

        if (decision.IsFinal &&
            (string.IsNullOrWhiteSpace(criteria.ApprovedProtocolBinding) ||
                string.IsNullOrWhiteSpace(criteria.ApprovedProtocolDigest) ||
                string.IsNullOrWhiteSpace(criteria.ApprovedProtocolDigestScope) ||
                string.IsNullOrWhiteSpace(criteria.ApprovedProtocolStatus)))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingCriteriaDigest, "Final criteria must be protocol-bound.");
        }

        if (decision.IsFinal)
        {
            EnsureFinalCriteriaProtocolBinding(criteria);
        }

        if (!string.Equals(
                criteria.DigestScopeHint ?? DigestScope.CanonicalJsonRecord.ToString(),
                DigestScope.CanonicalJsonRecord.ToString(),
                StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.InvalidCriteriaDigestScope,
                "Criteria digest scope is not canonical-json-record.");
        }

        var expectedDigest = criteria.ComputeDigest().ToString();
        if (!string.Equals(expectedDigest, decision.CriteriaDigest, StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.CriteriaDigestMismatch, "Decision criteria digest does not match approved criteria.");
        }
    }

    private static string BuildCriteriaKey(string criteriaId, string stage)
    {
        return $"{criteriaId}::{stage}";
    }

    private static void EnsureFinalCriteriaProtocolBinding(ScreeningCriteria criteria)
    {
        if (!string.Equals(criteria.ApprovedProtocolDigestScope, DigestScope.ProtocolContent.ToString(), StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.InvalidCriteriaDigestScope,
                "Final Screening criteria must bind a protocol-content digest.");
        }

        if (!ScreeningProtocolBindingStatus.IsApproved(criteria.ApprovedProtocolStatus))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.CriteriaDigestMismatch,
                "Final Screening criteria must bind an approved protocol version.");
        }

        if (!ContentDigest.TryParse(criteria.ApprovedProtocolDigest, out var approvedDigest))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.CriteriaDigestMismatch,
                "Final Screening criteria protocol digest must be a canonical content digest.");
        }

        if (!string.IsNullOrWhiteSpace(criteria.CurrentProtocolContentDigest))
        {
            if (!ContentDigest.TryParse(criteria.CurrentProtocolContentDigest, out var currentDigest))
            {
                throw new ScreeningRuleException(
                    ScreeningErrorCodes.CriteriaDigestMismatch,
                    "Current protocol digest must be a canonical content digest.");
            }

            if (approvedDigest != currentDigest)
            {
                throw new ScreeningRuleException(
                    ScreeningErrorCodes.CriteriaDigestMismatch,
                    "Final Screening criteria protocol binding is stale or digest-mismatched.");
            }
        }
    }

    private void EnsureAdjudicationResolution(ScreeningDecision decision)
    {
        if (string.IsNullOrWhiteSpace(decision.ResolvedConflictId))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.AdjudicationSourceMismatch,
                "Adjudication must reference the resolved conflict id.");
        }

        if (!_conflicts.TryGetValue(decision.ResolvedConflictId, out var conflict) || conflict.Resolved)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.AdjudicationSourceMismatch, "Adjudication conflict reference is missing or already resolved.");
        }

        if (!string.Equals(conflict.CandidateSetId, decision.CandidateSetId, StringComparison.Ordinal) ||
            !string.Equals(conflict.CandidateId, decision.CandidateId, StringComparison.Ordinal) ||
            !string.Equals(conflict.Stage, decision.Stage, StringComparison.Ordinal) ||
            !string.Equals(conflict.CriteriaDigest, decision.CriteriaDigest, StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.AdjudicationSourceMismatch,
                "Adjudication must match the conflicted stage, candidate, and criteria.");
        }

        if (decision.SourceDecisionIds.Count == 0)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Adjudication must preserve source decision ids.");
        }

        var sourceDecisionIds = decision.SourceDecisionIds.Distinct(StringComparer.Ordinal).ToArray();
        var conflictSourceSet = conflict.SourceDecisionIds.Distinct(StringComparer.Ordinal).ToArray();

        var missingIds = sourceDecisionIds.Except(conflictSourceSet, StringComparer.Ordinal).ToArray();
        var missingRequired = conflictSourceSet.Except(sourceDecisionIds, StringComparer.Ordinal).ToArray();
        if (missingIds.Length != 0 || missingRequired.Length != 0)
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.MissingSourceDecision,
                "Adjudication source decisions must preserve conflict source decision ids.");
        }

        foreach (var sourceId in sourceDecisionIds)
        {
            var sourceDecision = _decisions.FirstOrDefault(item => string.Equals(item.DecisionId, sourceId, StringComparison.Ordinal));
            if (sourceDecision is null)
            {
                throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Adjudication references unknown source decisions.");
            }

            if (!sourceDecision.IsFinal)
            {
                throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Adjudication source decisions must be final.");
            }

            if (!string.Equals(sourceDecision.CandidateId, decision.CandidateId, StringComparison.Ordinal)
                || !string.Equals(sourceDecision.Stage, decision.Stage, StringComparison.Ordinal)
                || !string.Equals(sourceDecision.CriteriaDigest, decision.CriteriaDigest, StringComparison.Ordinal))
            {
                throw new ScreeningRuleException(
                    ScreeningErrorCodes.AdjudicationSourceMismatch,
                    "Adjudication source decision scope must match the adjudication scope.");
            }
        }

        ResolveConflict(decision);
    }

    private static void EnsureNoProjectionIdentity(
        string decisionId,
        string? conflictId,
        IReadOnlyList<string> evidenceRefs,
        IReadOnlyList<string>? sourceDecisionIds = null,
        IReadOnlyList<string>? sourceSuggestionIds = null,
        string? stage = null)
    {
        var ids = new List<string> { decisionId };

        if (!string.IsNullOrWhiteSpace(conflictId))
        {
            ids.Add(conflictId);
        }

        if (sourceDecisionIds is not null)
        {
            ids.AddRange(sourceDecisionIds);
        }

        if (sourceSuggestionIds is not null)
        {
            ids.AddRange(sourceSuggestionIds);
        }

        if (string.Equals(stage, ScreeningStages.HumanAdjudication, StringComparison.Ordinal))
        {
            ids.Add("human_adjudication");
        }

        if (ids.Any(IsAppProjectionId))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.AppProjectionNotCoreAuthority,
                "App projection identifiers cannot be used as Core screening identities.");
        }

        if (evidenceRefs.Any(IsAppProjectionId))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.AppProjectionNotCoreAuthority,
                "App projection rows and artifacts cannot be treated as Core screening authority.");
        }
    }

    private static void EnsureNoPathIdentity(IReadOnlyList<string> evidenceRefs)
    {
        if (evidenceRefs.Any(IsLocalPathLike))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.LocalPathNotArtifactIdentity,
                "Local file path evidence must not be used as Core screening evidence identity.");
        }
    }

    private static bool IsAppProjectionId(string value)
    {
        return value.Contains("web-", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("assignment", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("batch", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("audit", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("cli", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("conflict-row", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("screening_conflict", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("screening-conflict", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("project_screening_conflicts", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("screening_batch", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("screening-batch", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("screening_audit", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("screening-audit", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("run-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalPathLike(string value)
    {
        if (value.StartsWith("artifact:", StringComparison.Ordinal) ||
            value.StartsWith("raw-artifact-bytes:", StringComparison.Ordinal))
        {
            return false;
        }

        return value.IndexOf('\\') >= 0 ||
            value.IndexOf('/') >= 0 ||
            (value.Length >= 3 && value[1] == ':') ||
            value.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".ris", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".bib", StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfUnresolvedConflictBlocksDownstream(ScreeningDecision decision)
    {
        var stageOrdinal = ScreeningStages.Ordinal(decision.Stage);

        if (stageOrdinal <= 0)
        {
            return;
        }

        var blocks = _conflicts.Values
            .Where(conflict => !conflict.Resolved &&
                string.Equals(conflict.CandidateSetId, decision.CandidateSetId, StringComparison.Ordinal) &&
                string.Equals(conflict.CandidateId, decision.CandidateId, StringComparison.Ordinal) &&
                ScreeningStages.Ordinal(conflict.Stage) < stageOrdinal)
            .ToArray();

        if (blocks.Length > 0)
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.UnresolvedConflict,
                "Downstream screening stage decisions are blocked by unresolved prior-stage conflicts.");
        }
    }

    private void UpsertConflictForKey(ScreeningDecision decision)
    {
        if (_conflicts.Values.Any(existing =>
            existing.Resolved &&
            string.Equals(existing.CandidateSetId, decision.CandidateSetId, StringComparison.Ordinal) &&
            string.Equals(existing.CandidateId, decision.CandidateId, StringComparison.Ordinal) &&
            string.Equals(existing.Stage, decision.Stage, StringComparison.Ordinal) &&
            string.Equals(existing.CriteriaDigest, decision.CriteriaDigest, StringComparison.Ordinal)))
        {
            return;
        }

        var sourceDecisions = _decisions
            .Where(item =>
                item.DecisionKind is ScreeningDecisionKind.Human &&
                string.Equals(item.CandidateSetId, _candidateSet.CandidateSetId, StringComparison.Ordinal) &&
                string.Equals(item.CandidateId, decision.CandidateId, StringComparison.Ordinal) &&
                string.Equals(item.Stage, decision.Stage, StringComparison.Ordinal) &&
                string.Equals(item.CriteriaDigest, decision.CriteriaDigest, StringComparison.Ordinal))
            .ToArray();

        var distinctVerdicts = sourceDecisions.Select(item => item.Verdict)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (sourceDecisions.Length < 2 || distinctVerdicts.Length <= 1)
        {
            return;
        }

        var sourceDecisionIds = sourceDecisions
            .Select(item => item.DecisionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var existingOpen = _conflicts.Values.FirstOrDefault(
            existing => !existing.Resolved &&
                string.Equals(existing.CandidateSetId, decision.CandidateSetId, StringComparison.Ordinal) &&
                string.Equals(existing.CandidateId, decision.CandidateId, StringComparison.Ordinal) &&
                string.Equals(existing.Stage, decision.Stage, StringComparison.Ordinal) &&
                string.Equals(existing.CriteriaDigest, decision.CriteriaDigest, StringComparison.Ordinal));

        var conflictId = BuildConflictId(
            _candidateSet.CandidateSetId,
            decision.CandidateId,
            decision.Stage,
            decision.CriteriaDigest,
            sourceDecisionIds);

        if (existingOpen is null)
        {
            _conflicts[conflictId] = new ScreeningConflict(
                conflictId,
                _candidateSet.CandidateSetId,
                decision.CandidateId,
                decision.Stage,
                decision.CriteriaDigest,
                sourceDecisionIds,
                Resolved: false);
            return;
        }

        if (_conflicts.ContainsKey(existingOpen.ConflictId))
        {
            _conflicts[existingOpen.ConflictId] = existingOpen with { SourceDecisionIds = sourceDecisionIds };
        }
    }

    private void ResolveConflict(ScreeningDecision decision)
    {
        if (decision.ResolvedConflictId is null)
        {
            return;
        }

        if (!_conflicts.TryGetValue(decision.ResolvedConflictId, out var conflict))
        {
            return;
        }

        var resolved = conflict with
        {
            Resolved = true,
            ResolvedAt = decision.DecidedAt,
            ResolvedByDecisionId = decision.DecisionId,
            ResolutionRationale = decision.Rationale
        };
        _conflicts[resolved.ConflictId] = resolved;
    }

    private static void ThrowIfMissingRequiredFullTextEvidence(IReadOnlyList<string> evidenceRefs, string targetRef)
    {
        if (!evidenceRefs.Any(IsFullTextArtifactEvidenceRef))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.FullTextArtifactRequired,
                $"{targetRef} requires evidence refs for full-text screening.");
        }
    }

    private static bool IsFullTextArtifactEvidenceRef(string value)
    {
        const string rawArtifactBytesPrefix = "raw-artifact-bytes:";
        const string artifactDigestMarker = "@raw-artifact-bytes:";

        if (value.StartsWith(rawArtifactBytesPrefix, StringComparison.Ordinal))
        {
            return ContentDigest.TryParse(value[rawArtifactBytesPrefix.Length..], out _);
        }

        if (value.StartsWith("artifact:", StringComparison.Ordinal))
        {
            var markerIndex = value.IndexOf(artifactDigestMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var digestStart = markerIndex + artifactDigestMarker.Length;
            return digestStart < value.Length &&
                ContentDigest.TryParse(value[digestStart..], out _);
        }

        return false;
    }

    private static string BuildConflictId(
        string candidateSetId,
        string candidateId,
        string stage,
        string criteriaDigest,
        IReadOnlyList<string> sourceDecisionIds)
    {
        var payload = $"{candidateSetId}|{candidateId}|{stage}|{criteriaDigest}|{string.Join('|', sourceDecisionIds)}";
        var digest = ContentDigest.Sha256Utf8(payload);
        return $"conflict:{digest}";
    }

    private const string ScreeningRuleConstantsNoPhpCompat = "no-php-compatibility-claim";
}
