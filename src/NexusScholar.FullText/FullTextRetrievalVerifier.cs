using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using NexusScholar.Kernel;

namespace NexusScholar.FullText;

public static class FullTextRetrievalVerifier
{
    public static FullTextRecordedRetrievalOutcome Verify(
        FullTextRecordedRetrievalEvidence evidence,
        byte[] exactBytes,
        FullTextRecordedRetrievalPolicy policy,
        string acquisitionId,
        string attemptId,
        string artifactId,
        DateTimeOffset? acquiredAt = null,
        FullTextActor? acquiredBy = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(exactBytes);
        if (policy is null)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Retrieval policy is required.");
        }

        var failure = ValidateEvidence(evidence, exactBytes, policy);
        if (failure is not null)
        {
            return BuildFailure(
                evidence,
                failure.Value.category,
                failure.Value.summary,
                acquisitionId,
                attemptId,
                policy.RetentionDisposition,
                acquiredAt,
                acquiredBy);
        }

        return BuildSuccess(
            evidence,
            exactBytes,
            policy,
            acquisitionId,
            attemptId,
            artifactId,
            acquiredAt,
            acquiredBy);
    }

    private static FullTextRecordedRetrievalOutcome BuildFailure(
        FullTextRecordedRetrievalEvidence evidence,
        string failureCategory,
        string? failureSummary,
        string acquisitionId,
        string attemptId,
        string retentionDisposition,
        DateTimeOffset? acquiredAt,
        FullTextActor? acquiredBy)
    {
        var metadata = BuildMetadata(evidence, failureCategory);
        var attempt = new FullTextSourceAttempt(
            Guard.NotBlank(attemptId, nameof(attemptId)),
            evidence.SourceAlias,
            1,
            FullTextAcquisitionKinds.OpenAccessSourceReference,
            FullTextAttemptStatuses.Failure,
            sourceUrl: evidence.SourceReference,
            sourceReference: evidence.SourceReference,
            errorCategory: failureCategory,
            errorMessage: string.IsNullOrWhiteSpace(failureSummary)
                ? "Recorded retrieval evidence failed verification."
                : failureSummary,
            sourceMetadata: metadata);

        var acquisition = new FullTextAcquisitionRecord(
            Guard.NotBlank(acquisitionId, nameof(acquisitionId)),
            evidence.Input,
            FullTextAcquisitionKinds.OpenAccessSourceReference,
            evidence.SourceAlias,
            evidence.SourceReference,
            acquiredBy,
            acquiredAt ?? evidence.ReceivedAt,
            FullTextAttemptStatuses.Failure,
            [attempt],
            sourceMetadata: metadata,
            nonClaims: [retentionDisposition, "recorded-fulltext-retrieval-evidence"]);

        return new FullTextRecordedRetrievalOutcome(
            false,
            acquisition,
            attempt,
            null,
            null,
            failureCategory,
            failureSummary);
    }

    private static FullTextRecordedRetrievalOutcome BuildSuccess(
        FullTextRecordedRetrievalEvidence evidence,
        byte[] exactBytes,
        FullTextRecordedRetrievalPolicy policy,
        string acquisitionId,
        string attemptId,
        string artifactId,
        DateTimeOffset? acquiredAt,
        FullTextActor? acquiredBy)
    {
        var metadata = BuildMetadata(evidence, null);
        var safeAcquiredAt = acquiredAt ?? evidence.ReceivedAt;

        var attempt = new FullTextSourceAttempt(
            Guard.NotBlank(attemptId, nameof(attemptId)),
            evidence.SourceAlias,
            1,
            FullTextAcquisitionKinds.OpenAccessSourceReference,
            FullTextAttemptStatuses.Success,
            sourceUrl: evidence.SourceReference,
            sourceReference: evidence.RightsReference,
            artifactKind: evidence.ArtifactKind,
            mediaType: evidence.MediaType,
            httpStatus: evidence.HttpStatus,
            sourceMetadata: metadata,
            artifactEvidenceId: Guard.NotBlank(artifactId, nameof(artifactId)));

        var acquisition = new FullTextAcquisitionRecord(
            Guard.NotBlank(acquisitionId, nameof(acquisitionId)),
            evidence.Input,
            FullTextAcquisitionKinds.OpenAccessSourceReference,
            evidence.SourceAlias,
            evidence.SourceReference,
            acquiredBy,
            safeAcquiredAt,
            FullTextAttemptStatuses.Success,
            [attempt],
            sourceMetadata: metadata,
            artifactEvidenceId: Guard.NotBlank(artifactId, nameof(artifactId)),
            nonClaims: [policy.RetentionDisposition, "recorded-fulltext-retrieval-evidence", "no-screening-authority"]);

        FullTextArtifactEvidence artifact;
        try
        {
            artifact = FullTextArtifactEvidence.FromBytes(
                Guard.NotBlank(artifactId, nameof(artifactId)),
                evidence.Input,
                acquisition,
                evidence.ArtifactKind,
                evidence.MediaType,
                exactBytes,
                policy.MaximumBytes);
        }
        catch (FullTextRuleException exception)
        {
            return BuildFailure(
                evidence,
                FullTextRetrievalErrorCodes.ConversionFailed,
                $"Recorded artifact conversion failed: {exception.Message}",
                acquisitionId,
                attemptId,
                policy.RetentionDisposition,
                acquiredAt,
                acquiredBy);
        }

        VerifiedFullTextChain chain;
        try
        {
            chain = FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(evidence.Input, acquisition, artifact, exactBytes, policy.MaximumBytes));
        }
        catch (FullTextRuleException exception)
        {
            return BuildFailure(
                evidence,
                FullTextRetrievalErrorCodes.ConversionFailed,
                $"Recorded artifact conversion failed: {exception.Message}",
                acquisitionId,
                attemptId,
                policy.RetentionDisposition,
                acquiredAt,
                acquiredBy);
        }

        return new FullTextRecordedRetrievalOutcome(
            true,
            chain.Acquisition,
            attempt,
            artifact,
            chain,
            null,
            null);
    }

    private static (string category, string? summary)? ValidateEvidence(
        FullTextRecordedRetrievalEvidence evidence,
        byte[] exactBytes,
        FullTextRecordedRetrievalPolicy policy)
    {
        if (!FullTextRetrievalAccessRoutes.IsKnown(evidence.AccessRoute))
        {
            return (FullTextRetrievalErrorCodes.AccessRouteMissing, "Recorded access route is not an allowed route.");
        }

        if (!FullTextRetrievalRights.IsAdmitted(evidence.RightsStatus))
        {
            return (FullTextRetrievalErrorCodes.RightsNotAdmitted,
                $"Recorded rights status '{evidence.RightsStatus}' does not permit conversion.");
        }

        if (evidence.RetentionDisposition != policy.RetentionDisposition)
        {
            return (FullTextRetrievalErrorCodes.InvalidEvidence, "Recorded retrieval retention disposition does not match policy.");
        }

        if (evidence.TerminalFailureCategory is not null)
        {
            return (evidence.TerminalFailureCategory, evidence.TerminalFailureSummary);
        }

        if (!string.Equals(evidence.SourceReference, evidence.SourceReference.Trim(), StringComparison.Ordinal))
        {
            return (FullTextRetrievalErrorCodes.InvalidUriPolicy, "Recorded source reference cannot contain leading/trailing whitespace.");
        }

        var redirectValidation = ValidateRedirectChain(evidence.SourceReference, evidence.RedirectChain, policy);
        if (redirectValidation is not null)
        {
            return redirectValidation;
        }

        if (!evidence.ResponseComplete)
        {
            return (FullTextRetrievalErrorCodes.IncompleteBody, "Recorded retrieval body is incomplete.");
        }

        if (!string.IsNullOrWhiteSpace(evidence.ContentEncoding) &&
            !string.Equals(evidence.ContentEncoding, "identity", StringComparison.OrdinalIgnoreCase))
        {
            return (FullTextRetrievalErrorCodes.UnsupportedEncoding, "Recorded retrieval body is encoded.");
        }

        if (exactBytes.LongLength != evidence.ByteLength)
        {
            return (FullTextRetrievalErrorCodes.ByteLengthMismatch,
                $"Recorded byte length {evidence.ByteLength} does not match supplied bytes {exactBytes.LongLength}.");
        }

        var expectedDigest = ContentDigest.Sha256(exactBytes).ToString();
        if (!string.Equals(expectedDigest, evidence.RawByteDigest, StringComparison.Ordinal))
        {
            return (FullTextRetrievalErrorCodes.DigestMismatch, "Recorded body digest does not match supplied bytes.");
        }

        if (exactBytes.LongLength > policy.MaximumBytes)
        {
            return (FullTextRetrievalErrorCodes.OversizedBody, "Recorded retrieval body exceeds policy limit.");
        }

        if (evidence.HttpStatus is < 200 or > 299)
        {
            return (FullTextErrorCodes.InaccessibleFullText, $"Recorded HTTP status {evidence.HttpStatus} is not successful.");
        }

        return null;
    }

    private static (string category, string? summary)? ValidateRedirectChain(
        string sourceReference,
        IReadOnlyCollection<FullTextRecordedRedirect> redirects,
        FullTextRecordedRetrievalPolicy policy)
    {
        var allUrls = new List<string>(1 + redirects.Count) { sourceReference };
        allUrls.AddRange(redirects.Select(item => item.RedirectUrl));
        foreach (var url in allUrls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                return (FullTextRetrievalErrorCodes.RedirectChainViolation, $"Recorded URL '{url}' is not an absolute URL.");
            }

            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return (FullTextRetrievalErrorCodes.RedirectChainViolation, $"Recorded URL '{url}' is not HTTPS.");
            }

            if (parsed.UserInfo.Length > 0)
            {
                return (FullTextRetrievalErrorCodes.RedirectChainViolation, $"Recorded URL '{url}' contains user information.");
            }

            if (!policy.AdmittedHosts.Contains(parsed.Host, StringComparer.OrdinalIgnoreCase))
            {
                return (FullTextRetrievalErrorCodes.RedirectChainViolation, $"Recorded URL '{url}' host '{parsed.Host}' is not admitted.");
            }

            if (!Uri.CheckHostName(parsed.Host).Equals(UriHostNameType.Dns) || IPAddress.TryParse(parsed.Host, out _))
            {
                return (FullTextRetrievalErrorCodes.InvalidUriPolicy, $"Recorded URL '{url}' host '{parsed.Host}' is not an admitted DNS host.");
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(
        FullTextRecordedRetrievalEvidence evidence,
        string? failureCategory)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["access_route"] = evidence.AccessRoute,
            ["access_rights_status"] = evidence.RightsStatus,
            ["access_rights_reference"] = evidence.RightsReference,
            ["content_type"] = evidence.MediaType,
            ["request_utc"] = CanonicalTimestamp.FormatUtc(evidence.RequestedAt),
            ["response_utc"] = CanonicalTimestamp.FormatUtc(evidence.ReceivedAt),
            ["retention_disposition"] = evidence.RetentionDisposition,
            ["source_reference"] = evidence.SourceReference,
            ["response_complete"] = evidence.ResponseComplete.ToString(CultureInfo.InvariantCulture),
            ["recorded_evidence_id"] = evidence.EvidenceId,
            ["redirect_count"] = evidence.RedirectChain.Count.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(failureCategory))
        {
            metadata["failure_category"] = failureCategory;
        }

        if (evidence.RedirectChain.Count > 0)
        {
            metadata["redirect_chain"] = string.Join(
                " ",
                evidence.RedirectChain.Select(item => $"{item.StatusCode}:{item.RedirectUrl}"));
        }

        return new ReadOnlyDictionary<string, string>(metadata);
    }
}
