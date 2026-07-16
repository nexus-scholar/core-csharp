using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using NexusScholar.Artifacts;
using NexusScholar.Kernel;

namespace NexusScholar.FullText;

public static class FullTextSchemas
{
    public const string InputSchemaId = "nexus.fulltext.input";
    public const string AcquisitionRecordSchemaId = "nexus.fulltext.acquisition-record";
    public const string ArtifactEvidenceSchemaId = "nexus.fulltext.artifact-evidence";
    public const string ExtractionRecordSchemaId = "nexus.fulltext.extraction-record";
    public const string EvidenceLocationSchemaId = "nexus.fulltext.evidence-location";
    public const string SchemaVersion = "1.0.0";
}

public static class FullTextSourceKinds
{
    public const string ScreeningHandoff = "screening-handoff";
    public const string LockedReviewableCandidateSet = "locked-reviewable-candidate-set";
    public const string RawSearchTrace = "raw-search-trace";
    public const string RawSearchImport = "raw-search-import";
    public const string RawDedupMember = "raw-dedup-member";

    public static bool IsAllowedInput(string sourceKind) =>
        string.Equals(sourceKind, ScreeningHandoff, StringComparison.Ordinal) ||
        string.Equals(sourceKind, LockedReviewableCandidateSet, StringComparison.Ordinal);
}

public static class FullTextScreeningVerdicts
{
    public const string Include = "include";
    public const string Exclude = "exclude";
    public const string NeedsReview = "needs_review";
}

public static class FullTextEligibility
{
    public const string Retrievable = "retrievable";
    public const string ReviewableRetrievable = "reviewable-retrievable";

    public static bool IsAllowed(string eligibility) =>
        string.Equals(eligibility, Retrievable, StringComparison.Ordinal) ||
        string.Equals(eligibility, ReviewableRetrievable, StringComparison.Ordinal);
}

public static class FullTextAcquisitionKinds
{
    public const string UserUploadedFile = "user-uploaded-file";
    public const string UserSuppliedLocalFile = "user-supplied-local-file";
    public const string ManualAcquisition = "manual-acquisition";
    public const string DeterministicStubArtifact = "deterministic-stub-artifact";
    public const string ExternalUrlReference = "external-url-reference";
    public const string DoiOrLandingPageReference = "doi-or-landing-page-reference";
    public const string OpenAccessSourceReference = "open-access-source-reference";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        UserUploadedFile,
        UserSuppliedLocalFile,
        ManualAcquisition,
        DeterministicStubArtifact,
        ExternalUrlReference,
        DoiOrLandingPageReference,
        OpenAccessSourceReference
    };

    public static bool IsAllowed(string acquisitionKind) => Allowed.Contains(acquisitionKind);

    public static bool RequiresActor(string acquisitionKind) =>
        string.Equals(acquisitionKind, UserUploadedFile, StringComparison.Ordinal) ||
        string.Equals(acquisitionKind, UserSuppliedLocalFile, StringComparison.Ordinal) ||
        string.Equals(acquisitionKind, ManualAcquisition, StringComparison.Ordinal);
}

public static class FullTextArtifactKinds
{
    public const string Pdf = "pdf";
    public const string Xml = "xml";
    public const string Text = "text";
    public const string DerivedText = "derived-text";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        Pdf,
        Xml,
        Text,
        DerivedText
    };

    public static bool IsAllowed(string artifactKind) => Allowed.Contains(artifactKind);
}

public static class FullTextAttemptStatuses
{
    public const string Success = "success";
    public const string Failure = "failure";
    public const string Skipped = "skipped";
    public const string ManualNeeded = "manual_needed";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        Success,
        Failure,
        Skipped,
        ManualNeeded
    };

    public static bool IsAllowed(string status) => Allowed.Contains(status);
}

public static class FullTextExtractionStatuses
{
    public const string Success = "success";
    public const string Failure = "failure";
    public const string Partial = "partial";
    public const string Skipped = "skipped";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        Success,
        Failure,
        Partial,
        Skipped
    };

    public static bool IsAllowed(string status) => Allowed.Contains(status);
}

public static class FullTextExtractionRepresentations
{
    public const string PageText = "page-text";
    public const string Sections = "sections";

    public static bool IsAllowed(string? value) =>
        string.Equals(value, PageText, StringComparison.Ordinal) ||
        string.Equals(value, Sections, StringComparison.Ordinal);
}

public static class FullTextErrorCodes
{
    public const string MissingFullText = "missing-full-text";
    public const string InaccessibleFullText = "inaccessible-full-text";
    public const string UnsupportedFileType = "unsupported-file-type";
    public const string UnsupportedAcquisitionKind = "unsupported-acquisition-kind";
    public const string MissingCandidateBinding = "missing-candidate-binding";
    public const string RawSearchTraceNotFullTextInput = "raw-search-trace-not-fulltext-input";
    public const string RawDedupRecordNotFullTextInput = "raw-dedup-record-not-fulltext-input";
    public const string ExcludedCandidateNotRetrievable = "excluded-candidate-not-retrievable";
    public const string NoPrimaryId = "no-primary-id";
    public const string MissingHumanOrImportActor = "missing-human-or-import-actor";
    public const string MissingRawArtifactDigest = "missing-raw-artifact-digest";
    public const string InvalidRawArtifactDigestScope = "invalid-raw-artifact-digest-scope";
    public const string RawArtifactDigestMismatch = "raw-artifact-digest-mismatch";
    public const string LocalPathNotArtifactIdentity = "local-path-not-artifact-identity";
    public const string AppProjectionNotCoreAuthority = "app-projection-not-core-authority";
    public const string ClosedOrNonOaSource = "closed-or-non-oa-source";
    public const string PaywallBypassForbidden = "paywall-bypass-forbidden";
    public const string ShadowLibraryForbidden = "shadow-library-forbidden";
    public const string ScrapingForbidden = "scraping-forbidden";
    public const string InvalidPdfSignature = "invalid-pdf-signature";
    public const string InvalidMediaType = "invalid-media-type";
    public const string InvalidXml = "invalid-xml";
    public const string HtmlNotFullTextXml = "html-not-fulltext-xml";
    public const string EmptyTextArtifact = "empty-text-artifact";
    public const string ArtifactTooLarge = "artifact-too-large";
    public const string DuplicateArtifact = "duplicate-artifact";
    public const string ExtractionFailure = "extraction-failure";
    public const string PartialExtraction = "partial-extraction";
    public const string DerivedTextMissingSourceDigest = "derived-text-missing-source-digest";
    public const string InvalidAuthorityChain = "invalid-fulltext-authority-chain";
    public const string InvalidAcquisitionState = "invalid-fulltext-acquisition-state";
    public const string InvalidLogicalPath = "invalid-fulltext-logical-path";
    public const string InvalidExtractionRepresentation = "invalid-extraction-representation";
    public const string ExtractionSourceMismatch = "extraction-source-mismatch";
}

public sealed class FullTextRuleException : InvalidOperationException
{
    public FullTextRuleException(string category, string message)
        : base(message)
    {
        Category = category;
    }

    public string Category { get; }
}

public sealed record FullTextSourceRef(string RefKind, string RefId)
{
    public string RefKind { get; } = Guard.NotBlank(RefKind, nameof(RefKind));

    public string RefId { get; } = Guard.NotBlank(RefId, nameof(RefId));
}

public sealed class FullTextInput
{
    internal FullTextInput(
        string inputId,
        string sourceKind,
        string candidateSetId,
        string candidateId,
        string eligibility,
        IReadOnlyList<FullTextSourceRef>? sourceRefs = null,
        string? screeningDecisionId = null,
        string? screeningStage = null,
        string? dedupResultId = null,
        string? dedupClusterId = null,
        string? workId = null,
        IReadOnlyList<string>? nonClaims = null)
    {
        InputId = Guard.NotBlank(inputId, nameof(inputId));
        SchemaId = FullTextSchemas.InputSchemaId;
        SchemaVersion = FullTextSchemas.SchemaVersion;
        SourceKind = Guard.NotBlank(sourceKind, nameof(sourceKind));
        CandidateSetId = Guard.NotBlank(candidateSetId, nameof(candidateSetId));
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        Eligibility = Guard.NotBlank(eligibility, nameof(eligibility));
        ScreeningDecisionId = string.IsNullOrWhiteSpace(screeningDecisionId) ? null : screeningDecisionId.Trim();
        ScreeningStage = string.IsNullOrWhiteSpace(screeningStage) ? null : screeningStage.Trim();
        DedupResultId = string.IsNullOrWhiteSpace(dedupResultId) ? null : dedupResultId.Trim();
        DedupClusterId = string.IsNullOrWhiteSpace(dedupClusterId) ? null : dedupClusterId.Trim();
        WorkId = string.IsNullOrWhiteSpace(workId) ? null : workId.Trim();
        SourceRefs = ToReadOnly(sourceRefs ?? Array.Empty<FullTextSourceRef>());
        NonClaims = ToReadOnly(nonClaims ?? DefaultNonClaims);

        ValidateInputBoundary(SourceKind, CandidateSetId, CandidateId);
        if (!FullTextSourceKinds.IsAllowedInput(SourceKind) || !FullTextEligibility.IsAllowed(Eligibility))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.MissingCandidateBinding,
                "Full Text input source kind and eligibility must be explicitly allowed.");
        }
    }

    public string InputId { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public string SourceKind { get; }

    public string CandidateSetId { get; }

    public string CandidateId { get; }

    public string? ScreeningDecisionId { get; }

    public string? ScreeningStage { get; }

    public string? DedupResultId { get; }

    public string? DedupClusterId { get; }

    public string? WorkId { get; }

    public string Eligibility { get; }

    public ReadOnlyCollection<FullTextSourceRef> SourceRefs { get; }

    public ReadOnlyCollection<string> NonClaims { get; }

    public static IReadOnlyList<string> DefaultNonClaims { get; } = new[]
    {
        "no-live-provider-network-behavior",
        "no-pdf-extraction-implementation",
        "no-broad-php-fulltext-compatibility",
        "no-path-runtime-projection-compatibility"
    };

    public static FullTextInput FromScreeningDecision(
        string inputId,
        string candidateSetId,
        string candidateId,
        string screeningDecisionId,
        string screeningStage,
        string verdict,
        bool needsReviewRetrievable = true,
        string? dedupResultId = null,
        string? dedupClusterId = null,
        string? workId = null,
        IReadOnlyList<FullTextSourceRef>? sourceRefs = null)
    {
        var eligibility = verdict switch
        {
            FullTextScreeningVerdicts.Include => FullTextEligibility.Retrievable,
            FullTextScreeningVerdicts.NeedsReview when needsReviewRetrievable => FullTextEligibility.ReviewableRetrievable,
            FullTextScreeningVerdicts.Exclude => throw new FullTextRuleException(
                FullTextErrorCodes.ExcludedCandidateNotRetrievable,
                "Final exclude decisions are not Full Text retrieval candidates by default."),
            _ => throw new FullTextRuleException(
                FullTextErrorCodes.ExcludedCandidateNotRetrievable,
                "Only include and allowed needs_review decisions are Full Text retrieval candidates.")
        };

        return new FullTextInput(
            inputId,
            FullTextSourceKinds.ScreeningHandoff,
            candidateSetId,
            candidateId,
            eligibility,
            sourceRefs,
            screeningDecisionId,
            screeningStage,
            dedupResultId,
            dedupClusterId,
            workId);
    }

    private static void ValidateInputBoundary(string sourceKind, string candidateSetId, string candidateId)
    {
        if (string.Equals(sourceKind, FullTextSourceKinds.RawSearchTrace, StringComparison.Ordinal) ||
            string.Equals(sourceKind, FullTextSourceKinds.RawSearchImport, StringComparison.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.RawSearchTraceNotFullTextInput,
                "Raw Search traces are evidence, not Full Text input.");
        }

        if (string.Equals(sourceKind, FullTextSourceKinds.RawDedupMember, StringComparison.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.RawDedupRecordNotFullTextInput,
                "Raw Deduplication member records are not Full Text input by themselves.");
        }

        if (string.IsNullOrWhiteSpace(candidateSetId) || string.IsNullOrWhiteSpace(candidateId))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.MissingCandidateBinding,
                "Full Text input must bind to a candidate set and candidate.");
        }
    }

    private static ReadOnlyCollection<T> ToReadOnly<T>(IReadOnlyList<T> items) => Array.AsReadOnly(items.ToArray());
}

public sealed record FullTextActor(string ActorId, string ActorKind)
{
    public string ActorId { get; } = Guard.NotBlank(ActorId, nameof(ActorId));

    public string ActorKind { get; } = Guard.NotBlank(ActorKind, nameof(ActorKind));
}

public static class FullTextActorKinds
{
    public const string Human = "human";
    public const string Import = "import";

    public static bool IsHumanOrImport(string actorKind) =>
        string.Equals(actorKind, Human, StringComparison.Ordinal) ||
        string.Equals(actorKind, Import, StringComparison.Ordinal);
}

public sealed class FullTextSourceAttempt
{
    public FullTextSourceAttempt(
        string attemptId,
        string sourceAlias,
        int attemptOrder,
        string acquisitionKind,
        string status,
        string? sourceUrl = null,
        string? sourceReference = null,
        string? artifactKind = null,
        string? mediaType = null,
        int? httpStatus = null,
        string? errorCategory = null,
        string? errorMessage = null,
        IReadOnlyDictionary<string, string>? sourceMetadata = null,
        string? artifactEvidenceId = null)
    {
        AttemptId = Guard.NotBlank(attemptId, nameof(attemptId));
        SourceAlias = Guard.NotBlank(sourceAlias, nameof(sourceAlias));
        AttemptOrder = attemptOrder > 0 ? attemptOrder : throw new ArgumentOutOfRangeException(nameof(attemptOrder));
        AcquisitionKind = Guard.NotBlank(acquisitionKind, nameof(acquisitionKind));
        Status = Guard.NotBlank(status, nameof(status));
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        ArtifactKind = string.IsNullOrWhiteSpace(artifactKind) ? null : artifactKind.Trim();
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        HttpStatus = httpStatus;
        ErrorCategory = string.IsNullOrWhiteSpace(errorCategory) ? null : errorCategory.Trim();
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        SourceMetadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(sourceMetadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        ArtifactEvidenceId = string.IsNullOrWhiteSpace(artifactEvidenceId) ? null : artifactEvidenceId.Trim();

        if (!FullTextAcquisitionKinds.IsAllowed(AcquisitionKind))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.UnsupportedAcquisitionKind,
                $"Unsupported acquisition kind '{AcquisitionKind}'.");
        }

        if (!FullTextAttemptStatuses.IsAllowed(Status))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.MissingFullText,
                $"Unsupported source attempt status '{Status}'.");
        }
    }

    public string AttemptId { get; }

    public string SourceAlias { get; }

    public int AttemptOrder { get; }

    public string AcquisitionKind { get; }

    public string? SourceUrl { get; }

    public string? SourceReference { get; }

    public string Status { get; }

    public string? ArtifactKind { get; }

    public string? MediaType { get; }

    public int? HttpStatus { get; }

    public string? ErrorCategory { get; }

    public string? ErrorMessage { get; }

    public ReadOnlyDictionary<string, string> SourceMetadata { get; }

    public string? ArtifactEvidenceId { get; }
}

public sealed class FullTextAcquisitionRecord
{
    internal FullTextAcquisitionRecord(
        string acquisitionId,
        FullTextInput inputRef,
        string acquisitionKind,
        string sourceAlias,
        string sourceReference,
        FullTextActor? acquiredBy,
        DateTimeOffset acquiredAt,
        string status,
        IReadOnlyList<FullTextSourceAttempt> sourceAttempts,
        string? sourceUrl = null,
        string? doiOrLandingPage = null,
        IReadOnlyDictionary<string, string>? sourceMetadata = null,
        string? artifactEvidenceId = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? nonClaims = null)
    {
        AcquisitionId = Guard.NotBlank(acquisitionId, nameof(acquisitionId));
        SchemaId = FullTextSchemas.AcquisitionRecordSchemaId;
        SchemaVersion = FullTextSchemas.SchemaVersion;
        InputRef = inputRef ?? throw new ArgumentNullException(nameof(inputRef));
        AcquisitionKind = Guard.NotBlank(acquisitionKind, nameof(acquisitionKind));
        SourceAlias = Guard.NotBlank(sourceAlias, nameof(sourceAlias));
        SourceReference = Guard.NotBlank(sourceReference, nameof(sourceReference));
        AcquiredBy = acquiredBy;
        AcquiredAt = acquiredAt;
        Status = Guard.NotBlank(status, nameof(status));
        SourceAttempts = Array.AsReadOnly((sourceAttempts ?? throw new ArgumentNullException(nameof(sourceAttempts))).ToArray());
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        DoiOrLandingPage = string.IsNullOrWhiteSpace(doiOrLandingPage) ? null : doiOrLandingPage.Trim();
        SourceMetadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(sourceMetadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        ArtifactEvidenceId = string.IsNullOrWhiteSpace(artifactEvidenceId) ? null : artifactEvidenceId.Trim();
        Warnings = Array.AsReadOnly((warnings ?? Array.Empty<string>()).ToArray());
        Errors = Array.AsReadOnly((errors ?? Array.Empty<string>()).ToArray());
        NonClaims = Array.AsReadOnly((nonClaims ?? FullTextInput.DefaultNonClaims).ToArray());

        Validate();
    }

    public string AcquisitionId { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public FullTextInput InputRef { get; }

    public string AcquisitionKind { get; }

    public string SourceAlias { get; }

    public string SourceReference { get; }

    public string? SourceUrl { get; }

    public string? DoiOrLandingPage { get; }

    public ReadOnlyDictionary<string, string> SourceMetadata { get; }

    public FullTextActor? AcquiredBy { get; }

    public DateTimeOffset AcquiredAt { get; }

    public string Status { get; }

    public ReadOnlyCollection<FullTextSourceAttempt> SourceAttempts { get; }

    public string? ArtifactEvidenceId { get; }

    public ReadOnlyCollection<string> Warnings { get; }

    public ReadOnlyCollection<string> Errors { get; }

    public ReadOnlyCollection<string> NonClaims { get; }

    private void Validate()
    {
        if (!FullTextAcquisitionKinds.IsAllowed(AcquisitionKind))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.UnsupportedAcquisitionKind,
                $"Unsupported acquisition kind '{AcquisitionKind}'.");
        }

        if (!FullTextAttemptStatuses.IsAllowed(Status))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.MissingFullText,
                $"Unsupported acquisition status '{Status}'.");
        }

        if (FullTextAcquisitionKinds.RequiresActor(AcquisitionKind) &&
            (AcquiredBy is null ||
                !FullTextActorKinds.IsHumanOrImport(AcquiredBy.ActorKind) ||
                AcquiredAt == default))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.MissingHumanOrImportActor,
                "User-supplied and manual Full Text acquisition requires a human or import actor and timestamp.");
        }

        if (SourceAttempts.Count > 0)
        {
            var ordered = SourceAttempts.Select(attempt => attempt.AttemptOrder).ToArray();
            if (ordered.Length != ordered.Distinct().Count())
            {
                throw new FullTextRuleException(
                    FullTextErrorCodes.MissingFullText,
                    "Source attempt order values must be unique.");
            }
        }
    }
}

public sealed class FullTextArtifactEvidence
{
    internal FullTextArtifactEvidence(
        string artifactId,
        FullTextInput inputRef,
        string candidateId,
        string acquisitionId,
        string acquisitionKind,
        string sourceAlias,
        string artifactKind,
        string mediaType,
        long sizeBytes,
        string rawByteDigest,
        string rawByteDigestScope,
        string validationStatus,
        byte[]? acceptedBytes = null,
        string? candidateSetId = null,
        string? screeningDecisionId = null,
        string? workId = null,
        string? dedupClusterId = null,
        string? sourceReference = null,
        IReadOnlyDictionary<string, string>? sourceMetadata = null,
        string? logicalPath = null,
        string? originalFileName = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? nonClaims = null)
    {
        ArtifactId = Guard.NotBlank(artifactId, nameof(artifactId));
        SchemaId = FullTextSchemas.ArtifactEvidenceSchemaId;
        SchemaVersion = FullTextSchemas.SchemaVersion;
        InputRef = inputRef ?? throw new ArgumentNullException(nameof(inputRef));
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        CandidateSetId = string.IsNullOrWhiteSpace(candidateSetId) ? null : candidateSetId.Trim();
        ScreeningDecisionId = string.IsNullOrWhiteSpace(screeningDecisionId) ? null : screeningDecisionId.Trim();
        WorkId = string.IsNullOrWhiteSpace(workId) ? null : workId.Trim();
        DedupClusterId = string.IsNullOrWhiteSpace(dedupClusterId) ? null : dedupClusterId.Trim();
        AcquisitionId = Guard.NotBlank(acquisitionId, nameof(acquisitionId));
        AcquisitionKind = Guard.NotBlank(acquisitionKind, nameof(acquisitionKind));
        SourceAlias = Guard.NotBlank(sourceAlias, nameof(sourceAlias));
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        SourceMetadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(sourceMetadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        ArtifactKind = Guard.NotBlank(artifactKind, nameof(artifactKind));
        MediaType = Guard.NotBlank(mediaType, nameof(mediaType));
        SizeBytes = sizeBytes;
        RawByteDigest = rawByteDigest ?? string.Empty;
        RawByteDigestScope = Guard.NotBlank(rawByteDigestScope, nameof(rawByteDigestScope));
        try
        {
            LogicalPath = string.IsNullOrWhiteSpace(logicalPath) ? null : ArtifactDescriptor.NormalizeLogicalPath(logicalPath);
        }
        catch (ArgumentException exception)
        {
            throw new FullTextRuleException(FullTextErrorCodes.InvalidLogicalPath, exception.Message);
        }
        OriginalFileName = string.IsNullOrWhiteSpace(originalFileName) ? null : originalFileName.Trim();
        ValidationStatus = Guard.NotBlank(validationStatus, nameof(validationStatus));
        Warnings = Array.AsReadOnly((warnings ?? Array.Empty<string>()).ToArray());
        Errors = Array.AsReadOnly((errors ?? Array.Empty<string>()).ToArray());
        NonClaims = Array.AsReadOnly((nonClaims ?? FullTextInput.DefaultNonClaims).ToArray());

        Validate(acceptedBytes);
    }

    public string ArtifactId { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public FullTextInput InputRef { get; }

    public string CandidateId { get; }

    public string? CandidateSetId { get; }

    public string? ScreeningDecisionId { get; }

    public string? WorkId { get; }

    public string? DedupClusterId { get; }

    public string AcquisitionId { get; }

    public string AcquisitionKind { get; }

    public string SourceAlias { get; }

    public string? SourceReference { get; }

    public ReadOnlyDictionary<string, string> SourceMetadata { get; }

    public string ArtifactKind { get; }

    public string MediaType { get; }

    public long SizeBytes { get; }

    public string RawByteDigest { get; }

    public string RawByteDigestScope { get; }

    public string? LogicalPath { get; }

    public string? OriginalFileName { get; }

    public string ValidationStatus { get; }

    public ReadOnlyCollection<string> Warnings { get; }

    public ReadOnlyCollection<string> Errors { get; }

    public ReadOnlyCollection<string> NonClaims { get; }

    public static FullTextArtifactEvidence FromBytes(
        string artifactId,
        FullTextInput inputRef,
        FullTextAcquisitionRecord acquisition,
        string artifactKind,
        string mediaType,
        byte[] acceptedBytes,
        long maxBytes,
        string? logicalPath = null,
        string? originalFileName = null)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(acceptedBytes);

        if (!FullTextAuthorityValidator.SameInput(inputRef, acquisition.InputRef))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidAuthorityChain,
                "Full Text acquisition does not belong to the supplied input.");
        }

        FullTextArtifactValidator.Validate(artifactKind, acceptedBytes, maxBytes, mediaType);
        var digest = ContentDigest.Sha256(acceptedBytes).ToString();

        return new FullTextArtifactEvidence(
            artifactId,
            inputRef,
            inputRef.CandidateId,
            acquisition.AcquisitionId,
            acquisition.AcquisitionKind,
            acquisition.SourceAlias,
            artifactKind,
            mediaType,
            acceptedBytes.LongLength,
            digest,
            DigestScope.RawArtifactBytes.ToString(),
            FullTextAttemptStatuses.Success,
            acceptedBytes,
            candidateSetId: inputRef.CandidateSetId,
            screeningDecisionId: inputRef.ScreeningDecisionId,
            workId: inputRef.WorkId,
            dedupClusterId: inputRef.DedupClusterId,
            logicalPath: logicalPath,
            originalFileName: originalFileName);
    }

    public static void RejectArtifactIdentityProjection(string projectionKind)
    {
        var normalized = Guard.NotBlank(projectionKind, nameof(projectionKind));
        if (normalized.Contains("path", StringComparison.OrdinalIgnoreCase))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.LocalPathNotArtifactIdentity,
                "Local paths, storage paths, and manifest paths are projections, not artifact identity.");
        }

        throw new FullTextRuleException(
            FullTextErrorCodes.AppProjectionNotCoreAuthority,
            "Application rows, routes, and projection ids are not Core artifact authority.");
    }

    private void Validate(byte[]? acceptedBytes)
    {
        if (!FullTextArtifactKinds.IsAllowed(ArtifactKind))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.UnsupportedFileType,
                $"Unsupported Full Text artifact kind '{ArtifactKind}'.");
        }

        if (!string.Equals(RawByteDigestScope, DigestScope.RawArtifactBytes.ToString(), StringComparison.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidRawArtifactDigestScope,
                "Full Text artifact evidence must use the raw-artifact-bytes digest scope.");
        }

        if (!ContentDigest.TryParse(RawByteDigest, out _))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.MissingRawArtifactDigest,
                "Raw byte digest must be a canonical sha256 digest.");
        }

        if (acceptedBytes is not null)
        {
            var actualDigest = ContentDigest.Sha256(acceptedBytes).ToString();
            if (!string.Equals(actualDigest, RawByteDigest, StringComparison.Ordinal))
            {
                throw new FullTextRuleException(
                    FullTextErrorCodes.RawArtifactDigestMismatch,
                    "Declared raw artifact digest does not match accepted bytes.");
            }

            if (SizeBytes != acceptedBytes.LongLength)
            {
                throw new FullTextRuleException(
                    FullTextErrorCodes.RawArtifactDigestMismatch,
                    "Declared artifact size does not match accepted bytes.");
            }
        }
    }
}

public static class FullTextArtifactValidator
{
    private static readonly HashSet<string> PdfMediaTypes = new(StringComparer.Ordinal)
    {
        "application/pdf",
        "application/x-pdf",
        "application/octet-stream"
    };

    private static readonly HashSet<string> XmlMediaTypes = new(StringComparer.Ordinal)
    {
        "application/xml",
        "text/xml",
        "application/jats+xml",
        "application/octet-stream"
    };

    private static readonly HashSet<string> TextMediaTypes = new(StringComparer.Ordinal)
    {
        "text/plain",
        "application/octet-stream"
    };

    public static void Validate(string artifactKind, byte[] bytes, long maxBytes, string? mediaType = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        if (bytes.LongLength > maxBytes)
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.ArtifactTooLarge,
                "Artifact bytes exceed the configured maximum size.");
        }

        switch (artifactKind)
        {
            case FullTextArtifactKinds.Pdf:
                ValidateMediaType(mediaType, PdfMediaTypes);
                ValidatePdf(bytes);
                break;
            case FullTextArtifactKinds.Xml:
                ValidateMediaType(mediaType, XmlMediaTypes);
                ValidateXml(bytes);
                break;
            case FullTextArtifactKinds.Text:
            case FullTextArtifactKinds.DerivedText:
                ValidateMediaType(mediaType, TextMediaTypes);
                ValidateText(bytes);
                break;
            default:
                throw new FullTextRuleException(
                    FullTextErrorCodes.UnsupportedFileType,
                    $"Unsupported Full Text artifact kind '{artifactKind}'.");
        }
    }

    private static void ValidateMediaType(string? mediaType, HashSet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return;
        }

        var normalized = mediaType.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidMediaType,
                $"Media type '{mediaType}' is not allowed for this artifact kind.");
        }
    }

    private static void ValidatePdf(byte[] bytes)
    {
        var signature = Encoding.ASCII.GetBytes("%PDF-");
        if (bytes.Length < signature.Length || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidPdfSignature,
                "PDF artifacts must begin with the %PDF- signature.");
        }
    }

    private static void ValidateXml(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!text.StartsWith("<", StringComparison.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidXml,
                "XML full text must begin with an XML element or declaration.");
        }

        var lower = text.Length > 64 ? text[..64].ToLowerInvariant() : text.ToLowerInvariant();
        if (lower.StartsWith("<!doctype html", StringComparison.Ordinal) ||
            lower.StartsWith("<html", StringComparison.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.HtmlNotFullTextXml,
                "HTML pages are not accepted as Full Text XML artifacts.");
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            while (reader.Read())
            {
            }
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidXml,
                "XML full text must be parseable without network entity loading.");
        }
    }

    private static void ValidateText(byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(bytes)))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.EmptyTextArtifact,
                "Text artifacts must contain non-empty text.");
        }
    }
}

public sealed record FullTextDuplicateArtifact(
    string RawByteDigest,
    string RawByteDigestScope,
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> CandidateIds,
    string Category)
{
    public IReadOnlyList<string> ArtifactIds { get; } = Array.AsReadOnly(ArtifactIds.ToArray());

    public IReadOnlyList<string> CandidateIds { get; } = Array.AsReadOnly(CandidateIds.ToArray());
}

public static class FullTextDuplicatePolicy
{
    public static IReadOnlyList<FullTextDuplicateArtifact> FindDuplicateArtifacts(
        IReadOnlyList<FullTextArtifactEvidence> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        return artifacts
            .GroupBy(artifact => artifact.RawByteDigest, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new FullTextDuplicateArtifact(
                group.Key,
                DigestScope.RawArtifactBytes.ToString(),
                group.Select(artifact => artifact.ArtifactId).ToArray(),
                group.Select(artifact => artifact.CandidateId).Distinct(StringComparer.Ordinal).ToArray(),
                FullTextErrorCodes.DuplicateArtifact))
            .ToArray();
    }
}

public sealed class FullTextExtractionRecord
{
    internal FullTextExtractionRecord(
        string extractionId,
        string sourceArtifactId,
        string sourceRawByteDigest,
        string sourceRawByteDigestScope,
        string extractorId,
        string extractorVersion,
        DateTimeOffset extractedAt,
        string extractionKind,
        string status,
        string? extractedTextDigest = null,
        string? extractedTextDigestScope = null,
        IReadOnlyList<string>? pageText = null,
        IReadOnlyList<string>? sections = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? nonClaims = null,
        string? representationKind = null)
    {
        ExtractionId = Guard.NotBlank(extractionId, nameof(extractionId));
        SchemaId = FullTextSchemas.ExtractionRecordSchemaId;
        SchemaVersion = FullTextSchemas.SchemaVersion;
        SourceArtifactId = Guard.NotBlank(sourceArtifactId, nameof(sourceArtifactId));
        SourceRawByteDigest = Guard.NotBlank(sourceRawByteDigest, nameof(sourceRawByteDigest));
        SourceRawByteDigestScope = Guard.NotBlank(sourceRawByteDigestScope, nameof(sourceRawByteDigestScope));
        ExtractorId = Guard.NotBlank(extractorId, nameof(extractorId));
        ExtractorVersion = Guard.NotBlank(extractorVersion, nameof(extractorVersion));
        ExtractedAt = extractedAt;
        ExtractionKind = Guard.NotBlank(extractionKind, nameof(extractionKind));
        Status = Guard.NotBlank(status, nameof(status));
        ExtractedTextDigest = string.IsNullOrWhiteSpace(extractedTextDigest) ? null : extractedTextDigest.Trim();
        ExtractedTextDigestScope = string.IsNullOrWhiteSpace(extractedTextDigestScope) ? null : extractedTextDigestScope.Trim();
        PageText = Array.AsReadOnly((pageText ?? Array.Empty<string>()).ToArray());
        Sections = Array.AsReadOnly((sections ?? Array.Empty<string>()).ToArray());
        Warnings = Array.AsReadOnly((warnings ?? Array.Empty<string>()).ToArray());
        Errors = Array.AsReadOnly((errors ?? Array.Empty<string>()).ToArray());
        NonClaims = Array.AsReadOnly((nonClaims ?? FullTextInput.DefaultNonClaims).ToArray());
        RepresentationKind = string.IsNullOrWhiteSpace(representationKind) ? null : representationKind.Trim();

        Validate();
    }

    public string ExtractionId { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public string SourceArtifactId { get; }

    public string SourceRawByteDigest { get; }

    public string SourceRawByteDigestScope { get; }

    public string ExtractorId { get; }

    public string ExtractorVersion { get; }

    public DateTimeOffset ExtractedAt { get; }

    public string ExtractionKind { get; }

    public string Status { get; }

    public string? ExtractedTextDigest { get; }

    public string? ExtractedTextDigestScope { get; }

    public ReadOnlyCollection<string> PageText { get; }

    public ReadOnlyCollection<string> Sections { get; }

    public ReadOnlyCollection<string> Warnings { get; }

    public ReadOnlyCollection<string> Errors { get; }

    public ReadOnlyCollection<string> NonClaims { get; }

    public string? RepresentationKind { get; }

    public static ContentDigest ComputeRepresentationDigest(
        string representationKind,
        IReadOnlyList<string> values)
    {
        if (!FullTextExtractionRepresentations.IsAllowed(representationKind))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidExtractionRepresentation,
                "Extraction representation kind is unsupported.");
        }

        var canonical = new CanonicalJsonObject()
            .Add("representation_kind", representationKind)
            .Add("values", new CanonicalJsonArray(values.Select(CanonicalJsonValue.From)));
        return ContentDigest.Sha256CanonicalJson(canonical);
    }

    private void Validate()
    {
        if (!ContentDigest.TryParse(SourceRawByteDigest, out _))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.DerivedTextMissingSourceDigest,
                "Derived extraction records must bind to the source raw byte digest.");
        }

        if (!string.Equals(SourceRawByteDigestScope, DigestScope.RawArtifactBytes.ToString(), StringComparison.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.DerivedTextMissingSourceDigest,
                "Extraction source digest scope must be raw-artifact-bytes.");
        }

        if (!FullTextExtractionStatuses.IsAllowed(Status))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.ExtractionFailure,
                $"Unsupported extraction status '{Status}'.");
        }

        if (string.Equals(Status, FullTextExtractionStatuses.Partial, StringComparison.Ordinal) &&
            !Warnings.Contains(FullTextErrorCodes.PartialExtraction, StringComparer.Ordinal))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.PartialExtraction,
                "Partial extraction records must preserve a partial-extraction warning category.");
        }

        if (ExtractedTextDigest is not null)
        {
            if (!ContentDigest.TryParse(ExtractedTextDigest, out _))
            {
                throw new FullTextRuleException(
                    FullTextErrorCodes.ExtractionFailure,
                    "Extracted text digest must be a canonical sha256 digest when supplied.");
            }

            if (string.IsNullOrWhiteSpace(ExtractedTextDigestScope))
            {
                throw new FullTextRuleException(
                    FullTextErrorCodes.ExtractionFailure,
                    "Extracted text digest scope is required when extracted text digest is supplied.");
            }
        }

        var hasPageText = PageText.Count > 0;
        var hasSections = Sections.Count > 0;
        var hasSuccessfulContent = string.Equals(Status, FullTextExtractionStatuses.Success, StringComparison.Ordinal) ||
            string.Equals(Status, FullTextExtractionStatuses.Partial, StringComparison.Ordinal);
        if (hasSuccessfulContent &&
            (!FullTextExtractionRepresentations.IsAllowed(RepresentationKind) || hasPageText == hasSections ||
                (string.Equals(RepresentationKind, FullTextExtractionRepresentations.PageText, StringComparison.Ordinal) != hasPageText)))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidExtractionRepresentation,
                "Successful extraction records require exactly one matching text representation.");
        }

        if (!hasSuccessfulContent && (hasPageText || hasSections || RepresentationKind is not null))
        {
            throw new FullTextRuleException(
                FullTextErrorCodes.InvalidExtractionRepresentation,
                "Failed or skipped extraction records cannot carry extracted text representation.");
        }

        if (hasSuccessfulContent)
        {
            var values = hasPageText ? PageText : Sections;
            var expectedDigest = ComputeRepresentationDigest(RepresentationKind!, values).ToString();
            if (!string.Equals(ExtractedTextDigestScope, DigestScope.CanonicalJsonRecord.ToString(), StringComparison.Ordinal) ||
                !string.Equals(ExtractedTextDigest, expectedDigest, StringComparison.Ordinal))
            {
                throw new FullTextRuleException(
                    FullTextErrorCodes.InvalidExtractionRepresentation,
                    "Extraction digest must bind the exact canonical representation.");
            }
        }
    }
}
