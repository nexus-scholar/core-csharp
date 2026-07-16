using System.Text;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Screening;
using NexusScholar.Screening.CorpusSnapshots;
using NexusScholar.Screening.FullText;

namespace NexusScholar.Reporting;

public static class ReviewFlowProjector
{
    public static ReviewFlowProjection Project(ReviewSliceAuthorities source, IEnumerable<string>? disclosures = null, IEnumerable<string>? nonClaims = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateAuthorityBindings(source);

        var titleOutcomes = source.ScreeningJournal.Projection.Outcomes.Values.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
        var titleIncludes = titleOutcomes.Where(item => item.Verdict == ScreeningVerdicts.Include).Select(item => item.CandidateId).ToArray();
        var titleExcludes = titleOutcomes.Where(item => item.Verdict == ScreeningVerdicts.Exclude).ToArray();
        RejectUnsupportedOutcomes(titleOutcomes, ScreeningStages.TitleAbstract);

        var casesByCandidate = source.FullTextCases.GroupBy(item => item.Admission.CandidateId, StringComparer.Ordinal).ToArray();
        if (casesByCandidate.Any(group => group.Count() != 1))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Each title/abstract include can have at most one Full Text case.");
        var extras = casesByCandidate.Select(group => group.Key).Except(titleIncludes, StringComparer.Ordinal).ToArray();
        if (extras.Length != 0) throw Rule(ReportingErrorCodes.InvalidAuthority, "Full Text cases cannot be added for non-included candidates.");

        var gaps = titleIncludes.Except(casesByCandidate.Select(group => group.Key), StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .Select(item => new ReviewFlowGap("missing-full-text-case", item, "No terminal Full Text case is bound to this title/abstract include."))
            .ToArray();
        var fullOutcomes = source.FullTextCases.Select(item => ValidateFullTextCase(item, source)).ToArray();
        RejectUnsupportedOutcomes(fullOutcomes, ScreeningStages.FullText);
        var fullIncludes = fullOutcomes.Count(item => item.Verdict == ScreeningVerdicts.Include);
        var fullExcludes = fullOutcomes.Count(item => item.Verdict == ScreeningVerdicts.Exclude);

        var memberCount = source.ScreeningPolicy.Binding.GroupUnits.Sum(item => item.MemberCandidateIds.Count);
        var groupCount = source.ScreeningPolicy.Binding.GroupUnits.Count;
        var unresolvedCount = source.ScreeningPolicy.Binding.UnresolvedUnits.Count;
        var counts = new ReviewFlowCounts(memberCount + unresolvedCount, memberCount - groupCount, groupCount + unresolvedCount,
            titleIncludes.Length, titleExcludes.Length, fullIncludes, fullExcludes, fullIncludes);
        var titleReasons = Reasons(titleExcludes);
        var fullReasons = Reasons(fullOutcomes.Where(item => item.Verdict == ScreeningVerdicts.Exclude));
        var audit = new ReviewAuditCounts(
            source.ScreeningJournal.Projection.Conflicts.Count + source.FullTextCases.Sum(item => item.Journal.Projection.Conflicts.Count),
            source.ScreeningJournal.Decisions.Count(item => item.Kind == ScreeningConductDecisionKind.Adjudication) + source.FullTextCases.Sum(item => item.Journal.Decisions.Count(decision => decision.Kind == ScreeningConductDecisionKind.Adjudication)),
            source.ScreeningJournal.Decisions.Count(item => item.SupersedesDecisionDigest is not null) + source.FullTextCases.Sum(item => item.Journal.Decisions.Count(decision => decision.SupersedesDecisionDigest is not null)),
            source.ScreeningJournal.Invalidations.Count + source.FullTextCases.Sum(item => item.Journal.Invalidations.Count));
        return new ReviewFlowProjection(source, counts, titleReasons, fullReasons, audit, gaps,
            NormalizeText((disclosures ?? Array.Empty<string>()).Concat(Deviations(source).Select(item => item.Deviation.Disclosure))),
            NormalizeText(nonClaims));
    }

    public static VerifiedReviewFlowReport Finalize(ReviewFlowProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Gaps.Count != 0) throw Rule(ReportingErrorCodes.IncompleteSlice, "A final report requires one terminal Full Text case per title/abstract include.");
        if (projection.NonClaims.Count == 0)
            throw Rule(ReportingErrorCodes.IncompleteSlice, "A final report requires explicit non-claims.");
        if (Deviations(projection.Authorities).Any(item => item.BlocksFinalReporting))
            throw Rule(ReportingErrorCodes.IncompleteSlice, "An unresolved Protocol inconsistency blocks final reporting.");
        var c = projection.Counts;
        if (c.Identified != c.DuplicatesConsolidated + c.PostDedup ||
            c.PostDedup != c.TitleAbstractIncluded + c.TitleAbstractExcluded ||
            c.TitleAbstractIncluded != c.FullTextIncluded + c.FullTextExcluded || c.Included != c.FullTextIncluded ||
            projection.TitleAbstractReasons.Sum(item => item.Count) != c.TitleAbstractExcluded ||
            projection.FullTextReasons.Sum(item => item.Count) != c.FullTextExcluded)
            throw Rule(ReportingErrorCodes.ConservationFailure, "Review flow counts or exclusion reasons do not conserve.");
        var slice = BuildSliceEnvelope(projection.Authorities);
        var report = BuildReportEnvelope(projection, slice.ComputeDigest());
        return new VerifiedReviewFlowReport(projection, slice, report);
    }

    private static void ValidateAuthorityBindings(ReviewSliceAuthorities source)
    {
        var protocol = source.Protocol.Version;
        var workflow = source.Workflow;
        var binding = source.ScreeningPolicy.Binding;
        var policy = source.ScreeningPolicy.Policy;
        if (workflow.ProtocolVersionId != protocol.Id || workflow.ProtocolContentDigest != protocol.ContentDigest ||
            binding.SourceResultId != source.Deduplication.Result.ResultId || binding.SourceResultDigest != source.Deduplication.ResultDigest ||
            binding.SnapshotId != source.Snapshot.SnapshotId || binding.SnapshotRecordDigest != source.Snapshot.RecordDigest ||
            source.Snapshot.SourceResultDigest != source.Deduplication.ResultDigest || policy.ProtocolVersionId != protocol.Id ||
            policy.ProtocolContentDigest != protocol.ContentDigest)
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Report authorities do not share the same verified protocol, workflow, deduplication, snapshot, and conduct cut.");
        ScreeningCorpusBindingAuthority.VerifyConductPolicyBinding(binding, policy);
        if (source.ScreeningJournal.Policy.Digest != policy.Digest || source.ScreeningHandoff.PolicyDigest != policy.Digest ||
            source.ScreeningHandoff.JournalHeadDigest != source.ScreeningJournal.Projection.HeadDigest ||
            !source.ScreeningJournal.Projection.HandoffReady)
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Title/abstract conduct handoff is stale or mismatched.");
        if (!source.ScreeningHandoff.Outcomes.SequenceEqual(source.ScreeningJournal.Projection.Outcomes.Values.OrderBy(item => item.CandidateId, StringComparer.Ordinal)))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Title/abstract handoff outcomes are not current.");
        foreach (var item in source.ProvenanceEvents)
        {
            if (item.EventDigest != item.ToDigestEnvelope().ComputeDigest() ||
                item.ProtocolBinding is not null && (item.ProtocolBinding.ProtocolVersionId != protocol.Id || item.ProtocolBinding.ProtocolContentDigest != protocol.ContentDigest) ||
                item.WorkflowBinding is not null && (item.WorkflowBinding.WorkflowId != workflow.WorkflowId || item.WorkflowBinding.WorkflowDigest != workflow.WorkflowDigest))
                throw Rule(ReportingErrorCodes.InvalidAuthority, "Provenance event is stale or bound to another report authority.");
        }
        if (source.Amendments.Any(item => item.ProducedVersion.Version.Id != protocol.Id ||
            item.ProducedVersion.Version.ContentDigest != protocol.ContentDigest))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Protocol amendment does not produce the report protocol authority.");
        var waiverBindings = source.Waivers.Select(item => new ReportingSupplementalBinding(item.Waiver.WaiverId, item.WaiverDigest))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var amendmentBindings = source.Amendments.Select(item => new ReportingSupplementalBinding(item.Amendment.AmendmentId, item.AmendmentDigest))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        if (!waiverBindings.SequenceEqual(source.Workflow.WaiverBindings) || !amendmentBindings.SequenceEqual(source.Workflow.AmendmentBindings))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Report supplemental authorities do not match the exact verified Workflow bindings.");
        var deviations = Deviations(source);
        if (deviations.Select(item => item.Deviation.DeviationId).Distinct(StringComparer.Ordinal).Count() != deviations.Count ||
            deviations.Any(item => item.ProtocolVersion.Version.Id != protocol.Id || item.ProtocolVersion.Version.ContentDigest != protocol.ContentDigest))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Protocol deviations must be unique and bind the report Protocol authority.");
        if (source.RapidReviewProfile is not null &&
            (source.RapidReviewProfile.Record.WorkflowId != workflow.WorkflowId || source.RapidReviewProfile.Record.WorkflowDigest != workflow.WorkflowDigest ||
             source.RapidReviewProfile.Record.ProtocolVersionId != protocol.Id || source.RapidReviewProfile.Record.ProtocolContentDigest != protocol.ContentDigest))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Rapid Review profile does not bind the report Workflow and Protocol authorities.");
        if (source.RapidReviewProfile is not null &&
            (source.Workflow.TemplateId is null || source.RapidReviewProfile.Record.TemplateId != source.Workflow.TemplateId ||
             source.RapidReviewProfile.Record.TemplateVersion != source.Workflow.TemplateVersion ||
             source.RapidReviewProfile.Record.TemplateDigest != source.Workflow.TemplateDigest))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Rapid Review profile does not bind the exact report template authority.");
        foreach (var deviation in deviations.Where(item => item.Deviation.ProfileId is not null))
        {
            var shortcut = source.RapidReviewProfile?.Shortcuts.SingleOrDefault(item => item.ShortcutId == deviation.Deviation.ShortcutId);
            if (source.RapidReviewProfile is null || deviation.Deviation.ProfileId != source.RapidReviewProfile.Record.ProfileId ||
                deviation.Deviation.ProfileDigest != source.RapidReviewProfile.RecordDigest || shortcut is null ||
                !shortcut.AffectedRequirementRefs.Contains(deviation.Deviation.PlannedRequirementId, StringComparer.Ordinal) ||
                deviation.Deviation.Consequence != shortcut.Consequence || deviation.Deviation.MitigationApplied != shortcut.Mitigation ||
                deviation.Deviation.Disclosure != shortcut.ReportingDisclosure ||
                shortcut.RequiredMitigationArtifactRefs.Any(reference => !deviation.Deviation.MitigationEvidenceReferences.Any(
                    evidence => evidence.Kind == "mitigation-artifact" && evidence.Id == reference)))
                throw Rule(ReportingErrorCodes.InvalidAuthority, "Deviation Rapid Review shortcut binding is missing or stale.");
        }
        var generationRoles = source.WorkspaceCut.Generations.Where(item => item.CandidateId is null).Select(item => item.Role).ToArray();
        if (!generationRoles.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                ReviewGenerationRoles.RequiredSingletons.OrderBy(item => item, StringComparer.Ordinal)))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Workspace cut does not contain the exact required singleton generation roles.");
        var fullTextGenerationCandidates = source.WorkspaceCut.Generations.Where(item => item.Role == ReviewGenerationRoles.FullText)
            .Select(item => item.CandidateId).ToArray();
        if (source.WorkspaceCut.Generations.Any(item => item.Role == ReviewGenerationRoles.FullText != (item.CandidateId is not null)) ||
            !fullTextGenerationCandidates.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                source.FullTextCases.Select(item => item.Admission.CandidateId).OrderBy(item => item, StringComparer.Ordinal)))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Workspace Full Text generations do not exactly cover the represented cases.");
    }

    private static ScreeningConductOutcome ValidateFullTextCase(FullTextReviewCaseAuthorities item, ReviewSliceAuthorities source)
    {
        ArgumentNullException.ThrowIfNull(item);
        var admission = VerifiedFullTextAdmission.Create(source.ScreeningJournal, source.ScreeningHandoff, item.Admission.CandidateId);
        var chainInputDigest = ContentDigest.Sha256(FullTextAuthorityCanonicalCodec.Serialize(item.ArtifactChain.Input));
        var invalid = new List<string>();
        if (admission.Digest != item.Admission.Digest) invalid.Add("admission");
        if (chainInputDigest != admission.InputDigest) invalid.Add("input");
        if (item.Journal.Policy.AdmissionDigest != admission.Digest) invalid.Add("policy-admission");
        if (item.Journal.Policy.FullTextArtifactDigest != item.Handoff.FullTextArtifactDigest) invalid.Add("artifact");
        if (item.Handoff.PolicyDigest != item.Journal.Policy.Digest) invalid.Add("policy");
        if (item.Handoff.JournalHeadDigest != item.Journal.Projection.HeadDigest) invalid.Add("head");
        if (item.Journal.Policy.ProtocolVersionId != source.Protocol.Version.Id ||
            item.Journal.Policy.ProtocolContentDigest != source.Protocol.Version.ContentDigest) invalid.Add("protocol");
        if (!item.Journal.Projection.HandoffReady || item.Handoff.Outcomes.Count != 1) invalid.Add("terminal-outcome");
        var projectedOutcome = item.Journal.Projection.Outcomes.Values.SingleOrDefault();
        if (projectedOutcome is null || item.Handoff.Outcomes.Count != 1 ||
            item.Handoff.Outcomes[0].CandidateId != projectedOutcome.CandidateId ||
            item.Handoff.Outcomes[0].Verdict != projectedOutcome.Verdict ||
            item.Handoff.Outcomes[0].ExclusionReasonCode != projectedOutcome.ExclusionReasonCode ||
            !item.Handoff.Outcomes[0].SupportingDecisionDigests.SequenceEqual(projectedOutcome.SupportingDecisionDigests)) invalid.Add("outcome");
        if (invalid.Count != 0)
            throw Rule(ReportingErrorCodes.InvalidAuthority, $"Full Text authority mismatch: {string.Join(",", invalid)}.");
        if (!ContentDigest.TryParse(item.ArtifactChain.Artifact.RawByteDigest, out var artifactDigest) ||
            artifactDigest != item.Handoff.FullTextArtifactDigest)
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Full Text artifact digest does not match the conduct authority.");
        if ((item.Journal.Policy.ExtractionAttemptDigest is null) != (item.ExtractionAttempt is null) ||
            item.ExtractionAttempt is not null &&
            (item.Journal.Policy.ExtractionAttemptDigest != item.ExtractionAttempt.Digest ||
             item.Handoff.ExtractionAttemptDigest != item.ExtractionAttempt.Digest ||
             item.ExtractionAttempt.SourceArtifactId != item.ArtifactChain.Artifact.ArtifactId ||
             item.ExtractionAttempt.SourceRawByteDigest != artifactDigest))
            throw Rule(ReportingErrorCodes.InvalidAuthority, "Full Text extraction attempt is missing or does not bind the represented artifact and conduct.");
        var outcome = item.Handoff.Outcomes[0];
        if (outcome.CandidateId != admission.CandidateId) throw Rule(ReportingErrorCodes.InvalidAuthority, "Full Text outcome candidate is mismatched.");
        return outcome;
    }

    private static void RejectUnsupportedOutcomes(IEnumerable<ScreeningConductOutcome> outcomes, string stage)
    {
        foreach (var item in outcomes)
        {
            if (item.Verdict is not ScreeningVerdicts.Include and not ScreeningVerdicts.Exclude ||
                item.Verdict == ScreeningVerdicts.Exclude && string.IsNullOrWhiteSpace(item.ExclusionReasonCode) ||
                item.Verdict == ScreeningVerdicts.Include && item.ExclusionReasonCode is not null)
                throw Rule(ReportingErrorCodes.InvalidAuthority, $"{stage} has a non-terminal or invalid outcome.");
        }
    }

    private static IReadOnlyList<ReviewReasonCount> Reasons(IEnumerable<ScreeningConductOutcome> outcomes) => outcomes
        .GroupBy(item => item.ExclusionReasonCode!, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new ReviewReasonCount(group.Key, group.Count())).ToArray();

    private static IReadOnlyList<string> NormalizeText(IEnumerable<string>? values) => (values ?? Array.Empty<string>())
        .Select(value => value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>()
        .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static DigestEnvelope BuildSliceEnvelope(ReviewSliceAuthorities source)
    {
        var workflow = source.Workflow;
        var content = new CanonicalJsonObject()
            .Add("protocol_version_id", source.Protocol.Version.Id).Add("protocol_content_digest", source.Protocol.Version.ContentDigest.ToString())
            .Add("workflow_id", workflow.WorkflowId).Add("workflow_digest", workflow.WorkflowDigest.ToString())
            .Add("deduplication_result_id", source.Deduplication.Result.ResultId).Add("deduplication_result_digest", source.Deduplication.ResultDigest.ToString())
            .Add("snapshot_id", source.Snapshot.SnapshotId).Add("snapshot_record_digest", source.Snapshot.RecordDigest.ToString())
            .Add("screening_binding_digest", source.ScreeningPolicy.Binding.BindingDigest.ToString())
            .Add("screening_policy_digest", source.ScreeningPolicy.Policy.Digest.ToString()).Add("screening_handoff_digest", source.ScreeningHandoff.Digest.ToString())
            .Add("full_text_cases", FullTextBindings(source.FullTextCases))
            .Add("waiver_digests", DigestArray(source.Waivers.Select(item => item.WaiverDigest)))
            .Add("amendment_digests", DigestArray(source.Amendments.Select(item => item.AmendmentDigest)))
            .Add("deviation_digests", DigestArray(Deviations(source).Select(item => item.DeviationDigest)))
            .Add("provenance_event_digests", DigestArray(source.ProvenanceEvents.Select(item => item.EventDigest)))
            .Add("workspace_id", source.WorkspaceCut.WorkspaceId).Add("project_revision", source.WorkspaceCut.ProjectRevision)
            .Add("workspace_generations", CanonicalJsonValue.Array(source.WorkspaceCut.Generations.Select(item =>
            {
                var value = new CanonicalJsonObject().Add("role", item.Role).Add("generation_id", item.GenerationId)
                    .Add("manifest_digest", item.ManifestDigest.ToString());
                if (item.CandidateId is not null) value.Add("candidate_id", item.CandidateId);
                return value;
            }).ToArray())).Add("workspace_cut_digest", source.WorkspaceCut.Digest.ToString());
        if (source.RapidReviewProfile is not null) content.Add("rapid_review_profile_digest", source.RapidReviewProfile.RecordDigest.ToString());
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReportingSchemas.SliceBindingId, ReportingSchemas.Version, content);
    }

    private static DigestEnvelope BuildReportEnvelope(ReviewFlowProjection projection, ContentDigest sliceDigest)
    {
        var c = projection.Counts;
        var counts = new CanonicalJsonObject().Add("identified", c.Identified).Add("duplicates_consolidated", c.DuplicatesConsolidated)
            .Add("post_dedup", c.PostDedup).Add("title_abstract_included", c.TitleAbstractIncluded).Add("title_abstract_excluded", c.TitleAbstractExcluded)
            .Add("full_text_included", c.FullTextIncluded).Add("full_text_excluded", c.FullTextExcluded).Add("included", c.Included);
        var audit = new CanonicalJsonObject().Add("conflicts", projection.Audit.Conflicts).Add("adjudications", projection.Audit.Adjudications)
            .Add("corrections", projection.Audit.Corrections).Add("invalidations", projection.Audit.Invalidations);
        var source = projection.Authorities;
        var bindings = new CanonicalJsonObject().Add("slice_digest", sliceDigest.ToString())
            .Add("protocol_content_digest", source.Protocol.Version.ContentDigest.ToString())
            .Add("workflow_digest", source.Workflow.WorkflowDigest.ToString())
            .Add("deduplication_result_digest", source.Deduplication.ResultDigest.ToString())
            .Add("snapshot_record_digest", source.Snapshot.RecordDigest.ToString())
            .Add("screening_binding_digest", source.ScreeningPolicy.Binding.BindingDigest.ToString())
            .Add("screening_handoff_digest", source.ScreeningHandoff.Digest.ToString())
            .Add("full_text_cases", FullTextBindings(source.FullTextCases))
            .Add("waiver_digests", DigestArray(source.Waivers.Select(item => item.WaiverDigest)))
            .Add("amendment_digests", DigestArray(source.Amendments.Select(item => item.AmendmentDigest)))
            .Add("deviation_digests", DigestArray(Deviations(source).Select(item => item.DeviationDigest)))
            .Add("provenance_event_digests", DigestArray(source.ProvenanceEvents.Select(item => item.EventDigest)))
            .Add("workspace_cut_digest", source.WorkspaceCut.Digest.ToString());
        if (source.RapidReviewProfile is not null) bindings.Add("rapid_review_profile_digest", source.RapidReviewProfile.RecordDigest.ToString());
        var content = new CanonicalJsonObject().Add("bindings", bindings).Add("counts", counts)
            .Add("title_abstract_exclusion_reasons", ReasonArray(projection.TitleAbstractReasons))
            .Add("full_text_exclusion_reasons", ReasonArray(projection.FullTextReasons)).Add("audit_counts", audit)
            .Add("disclosures", TextArray(projection.Disclosures)).Add("non_claims", TextArray(projection.NonClaims));
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReportingSchemas.ReportId, ReportingSchemas.Version, content);
    }

    private static CanonicalJsonValue ReasonArray(IEnumerable<ReviewReasonCount> values) => CanonicalJsonValue.Array(values.Select(item =>
        new CanonicalJsonObject().Add("code", item.Code).Add("count", item.Count)).ToArray());
    private static CanonicalJsonValue FullTextBindings(IEnumerable<FullTextReviewCaseAuthorities> cases) => CanonicalJsonValue.Array(cases
        .OrderBy(item => item.Admission.CandidateId, StringComparer.Ordinal).Select(item =>
        {
            var value = new CanonicalJsonObject().Add("candidate_id", item.Admission.CandidateId)
                .Add("admission_digest", item.Admission.Digest.ToString()).Add("artifact_digest", item.Handoff.FullTextArtifactDigest.ToString())
                .Add("conduct_policy_digest", item.Journal.Policy.Digest.ToString()).Add("handoff_digest", item.Handoff.Digest.ToString());
            if (item.ExtractionAttempt is not null) value.Add("extraction_attempt_digest", item.ExtractionAttempt.Digest.ToString());
            return value;
        }).ToArray());
    private static CanonicalJsonValue DigestArray(IEnumerable<ContentDigest> values) => CanonicalJsonValue.Array(values.OrderBy(item => item.ToString(), StringComparer.Ordinal).Select(item => CanonicalJsonValue.From(item.ToString())).ToArray());
    private static CanonicalJsonValue TextArray(IEnumerable<string> values) => CanonicalJsonValue.Array(values.Select(CanonicalJsonValue.From).ToArray());
    private static IReadOnlyList<VerifiedProtocolDeviation> Deviations(ReviewSliceAuthorities source) => source.Deviations;
    private static ReportingRuleException Rule(string category, string message) => new(category, message);
}

public static class ReportingCanonicalCodec
{
    public static byte[] SerializeSlice(VerifiedReviewFlowReport report) => CanonicalJsonSerializer.SerializeToUtf8Bytes(report.SliceEnvelope.ToCanonicalJsonObject());
    public static byte[] SerializeReport(VerifiedReviewFlowReport report) => CanonicalJsonSerializer.SerializeToUtf8Bytes(report.ReportEnvelope.ToCanonicalJsonObject());

    public static VerifiedReviewFlowReport Rehydrate(byte[] sliceBytes, byte[] reportBytes, ReviewFlowProjection projection)
    {
        var expected = ReviewFlowProjector.Finalize(projection);
        if (!sliceBytes.SequenceEqual(SerializeSlice(expected)) || !reportBytes.SequenceEqual(SerializeReport(expected)))
            throw new ReportingRuleException(ReportingErrorCodes.NonCanonicalRecord, "Reporting records must match exact canonical bytes reconstructed from verified authorities.");
        return expected;
    }
}

public static class ReviewFlowMarkdownRenderer
{
    public static byte[] Render(VerifiedReviewFlowReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var c = report.Projection.Counts;
        var text = new StringBuilder().AppendLine("# Review Flow Report").AppendLine()
            .AppendLine($"Report digest: `{report.ReportDigest}`").AppendLine()
            .AppendLine("| Stage | Count |").AppendLine("| --- | ---: |")
            .AppendLine($"| Identified | {c.Identified} |").AppendLine($"| Duplicates consolidated | {c.DuplicatesConsolidated} |")
            .AppendLine($"| Post-deduplication | {c.PostDedup} |").AppendLine($"| Title/abstract included | {c.TitleAbstractIncluded} |")
            .AppendLine($"| Title/abstract excluded | {c.TitleAbstractExcluded} |").AppendLine($"| Full Text included | {c.FullTextIncluded} |")
            .AppendLine($"| Full Text excluded | {c.FullTextExcluded} |").AppendLine($"| Included | {c.Included} |");
        AppendList(text, "Disclosures", report.Projection.Disclosures);
        AppendList(text, "Non-claims", report.Projection.NonClaims);
        return Encoding.UTF8.GetBytes(text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static void AppendList(StringBuilder value, string heading, IReadOnlyList<string> items)
    {
        value.AppendLine().AppendLine($"## {heading}");
        foreach (var item in items) value.AppendLine($"- {item.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)}");
    }
}
