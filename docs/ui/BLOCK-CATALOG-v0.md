# Block Catalog v0

This catalog is a brainstorming document. It does not define final schemas or create implementation authority.

## Evidence Blocks

- `EvidenceCard`: Shows a preserved source record, raw evidence reference, digest, and source binding.
- `SourceSightingBlock`: Shows where a work or candidate appeared across search traces and imported exports.
- `RawEvidencePeekBlock`: Allows progressive disclosure from summary fields to raw source payload and digest metadata.
- `DigestBadge`: Compact evidence stability indicator with expandable audit details.

## Validation Blocks

- `ValidationErrorBlock`: Explains a Core validation failure and the next valid action.
- `ParserWarningBlock`: Shows import/parser warnings without discarding the affected raw record.
- `PolicyMismatchBlock`: Explains when a workflow action conflicts with an accepted policy, ADR, or criteria digest.
- `BlockingInvariantBlock`: Stops the workflow where a product law or domain rule prevents an action.

## Human Gate Blocks

- `HumanApprovalGate`: Requires an identified actor to accept a proposal or approve a decision.
- `MergeDecisionGate`: Requires human review for fuzzy, no-id, or source-specific duplicate candidates.
- `ScreeningDecisionGate`: Records include, exclude, or needs-review decisions by a human actor.
- `ProtocolApprovalGate`: Presents digest-bound protocol approval material.

## AI Proposal Blocks

- `AIProposalBlock`: Displays a model suggestion with evidence, prompt/response digest references when available, and acceptance boundary.
- `AIExplanationBlock`: Explains validation errors, parser warnings, or evidence relationships.
- `AIRationaleDraftBlock`: Drafts screening rationale text for human review.
- `AISearchQuerySuggestionBlock`: Suggests search strings or provider-specific query adjustments.

## Comparison Blocks

- `RecordComparisonBlock`: Compares candidate records across identifiers, title, abstract, source, year, and warnings.
- `IdentifierOverlapBlock`: Shows exact stable identifier overlap and namespace-specific conflicts.
- `TitleSimilarityBlock`: Shows title normalization, similarity score, threshold, and review requirement.
- `CriteriaComparisonBlock`: Compares screening criteria versions or digest material.

## Timeline And Replay Blocks

- `ProvenanceTimelineBlock`: Shows append-only events and decision lineage.
- `DecisionHistoryBlock`: Shows prior human decisions, AI suggestions, conflicts, and adjudications.
- `BundleReplayBlock`: Shows the replay path for an exported bundle.

## Search Blocks

- `SearchTraceBlock`: Shows query, provider, trace id, run metadata, warnings, and raw result count.
- `SearchPlanBlock`: Shows planned provider/search steps and validation status.
- `SearchResultEvidenceBlock`: Shows raw search result evidence without implying deduplicated corpus membership.

## Import Blocks

- `ImportSummaryBlock`: Summarizes imported files, parsed records, warnings, and evidence preservation.
- `ImportSourceBlock`: Shows imported source identity, file digest, parser, and record count.
- `ImportWarningBlock`: Shows rows with malformed identifiers, missing fields, or parser caveats.
- `ImportMappingBlock`: Shows how source columns mapped into local fields.

## Dedup Blocks

- `CandidateClusterBlock`: Shows automatic and review-required candidate groups.
- `RecordComparisonBlock`: Compares candidate records side by side or stacked.
- `IdentifierOverlapBlock`: Explains exact identifier evidence.
- `TitleSimilarityBlock`: Explains fuzzy-title evidence and threshold.
- `SourceSightingsBlock`: Shows all preserved sightings across search/import inputs.
- `MergeDecisionGate`: Presents accept, reject, unresolved, request more evidence, and open raw evidence actions.
- `ProvenancePreviewBlock`: Shows what decision/provenance would be recorded before action submission.

## Screening Blocks

- `ScreeningCard`: Shows candidate title, abstract, source evidence, and current stage.
- `CriteriaChecklistBlock`: Displays inclusion/exclusion criteria bound to a criteria digest.
- `EvidenceSummaryBlock`: Summarizes cited candidate evidence.
- `AIRationaleDraftBlock`: Drafts rationale text without final authority.
- `HumanScreeningDecisionBlock`: Captures include, exclude, or needs-review from an identified human.
- `ConflictWithProtocolBlock`: Explains protocol or criteria conflicts.

## Bundle Blocks

- `BundleVerificationBlock`: Shows export/import verification status and tamper checks.
- `BundleManifestBlock`: Shows manifest records and digests.
- `BundleWarningBlock`: Explains missing, mismatched, or non-authoritative records.

## First Prototype Candidate Set

The first prototype should focus on Import and Deduplication:

- `ImportSummaryBlock`
- `ImportWarningBlock`
- `SourceSightingBlock`
- `CandidateClusterBlock`
- `RecordComparisonBlock`
- `IdentifierOverlapBlock`
- `TitleSimilarityBlock`
- `AIExplanationBlock`
- `MergeDecisionGate`
- `ProvenancePreviewBlock`

This set demonstrates evidence preservation, review-required candidates, human authorization, and provenance preview without requiring Screening or full-text implementation first.
