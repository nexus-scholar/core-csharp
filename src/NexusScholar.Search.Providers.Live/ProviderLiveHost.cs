using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.Search.Providers.Live;

public sealed class ProviderLiveRequest
{
    private static readonly IReadOnlyDictionary<string, (string Provider, string Method, string Path)> Operations =
        new ReadOnlyDictionary<string, (string Provider, string Method, string Path)>(
            new Dictionary<string, (string Provider, string Method, string Path)>(StringComparer.Ordinal)
            {
                ["openalex.works"] = ("openalex", "GET", "/works"),
                ["semantic-scholar.bulk-search"] = ("semantic_scholar", "GET", "/graph/v1/paper/search/bulk"),
                ["semantic-scholar.paper-batch"] = ("semantic_scholar", "POST", "/graph/v1/paper/batch")
            });
    private readonly byte[]? body;

    private ProviderLiveRequest(
        string operationId,
        string providerAlias,
        string method,
        string endpointPathAndQuery,
        byte[]? body)
    {
        if (!Operations.TryGetValue(operationId, out var operation) ||
            !string.Equals(operation.Provider, providerAlias, StringComparison.Ordinal) ||
            !string.Equals(operation.Method, method, StringComparison.Ordinal))
        {
            throw Rule("Live provider operation, alias, or method is not admitted.");
        }

        if (!Uri.TryCreate($"https://placeholder.invalid{endpointPathAndQuery}", UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.AbsolutePath, operation.Path, StringComparison.Ordinal) ||
            parsed.UserInfo.Length > 0 ||
            parsed.Fragment.Length > 0 ||
            endpointPathAndQuery.Contains("://", StringComparison.Ordinal) ||
            endpointPathAndQuery.Contains('\\', StringComparison.Ordinal) ||
            endpointPathAndQuery.Split('?')[0].Split('/').Any(segment => segment is "." or ".."))
        {
            throw Rule("Live provider endpoint descriptor is not admitted.");
        }

        foreach (var pair in parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            if (ProviderSecretPolicy.ContainsForbiddenDescriptorValue(
                    name,
                    value,
                    allowPaginationToken: string.Equals(
                        operationId,
                        "semantic-scholar.bulk-search",
                        StringComparison.Ordinal)))
            {
                throw Rule("Live provider endpoint descriptor contains credential-shaped material.");
            }
        }

        var bodyBytes = body?.ToArray();
        if (method == "POST" != (bodyBytes is not null) ||
            bodyBytes is { Length: 0 })
        {
            throw Rule("Live provider request body does not match its admitted method.");
        }

        if (bodyBytes is not null)
        {
            var text = System.Text.Encoding.UTF8.GetString(bodyBytes);
            if (ContainsForbiddenValue(text))
            {
                throw Rule("Live provider request body contains credential-shaped material.");
            }
        }

        OperationId = operationId;
        ProviderAlias = providerAlias;
        Method = method;
        EndpointPathAndQuery = endpointPathAndQuery;
        this.body = bodyBytes;
    }

    public string OperationId { get; }
    public string ProviderAlias { get; }
    public string Method { get; }
    public string EndpointPathAndQuery { get; }
    public bool HasBody => body is not null;

    internal static ProviderLiveRequest Get(string operationId, string providerAlias, string endpointPathAndQuery) =>
        new(operationId, providerAlias, "GET", endpointPathAndQuery, null);

    internal static ProviderLiveRequest Post(
        string operationId,
        string providerAlias,
        string endpointPathAndQuery,
        byte[] body) =>
        new(operationId, providerAlias, "POST", endpointPathAndQuery, body);

    public byte[] CopySanitizedBody() => body?.ToArray() ??
        throw Rule("Live provider request does not carry a body.");

    public DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        "nexus.search.live-provider-request",
        "1.0.0",
        new CanonicalJsonObject()
            .Add("body_digest", body is null ? string.Empty : ContentDigest.Sha256(body).ToString())
            .Add("endpoint_path_and_query", EndpointPathAndQuery)
            .Add("method", Method)
            .Add("operation_id", OperationId)
            .Add("provider_alias", ProviderAlias));

    public ContentDigest Digest => Envelope().ComputeDigest();

    public static bool ContainsForbiddenValue(string value) =>
        ProviderSecretPolicy.ContainsForbiddenValue(value);

    private static SearchRuleException Rule(string message) =>
        new(ProviderAcquisitionErrorCodes.TransportPolicyViolation, message);
}

public sealed class ProviderLiveHttpResponse : IDisposable
{
    private byte[] body;
    private bool disposed;

    internal ProviderLiveHttpResponse(
        ProviderLiveRequest request,
        byte[] body,
        int statusCode,
        string mediaType,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string> observedHeaders)
    {
        Request = request;
        this.body = body;
        RawResponseDigest = ContentDigest.Sha256(body);
        ByteLength = body.LongLength;
        StatusCode = statusCode;
        MediaType = mediaType;
        RequestedAt = requestedAt;
        ReceivedAt = receivedAt;
        ObservedHeaders = observedHeaders;
    }

    public ProviderLiveRequest Request { get; }
    public ContentDigest RawResponseDigest { get; }
    public long ByteLength { get; }
    public bool HasBody => !disposed;
    public int StatusCode { get; }
    public string MediaType { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public IReadOnlyDictionary<string, string> ObservedHeaders { get; }

    public byte[] CopyBody()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return body.ToArray();
    }

    public void VerifyReceipt(byte[] candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.LongLength != ByteLength || ContentDigest.Sha256(candidate) != RawResponseDigest)
        {
            throw new SearchRuleException(
                ProviderAcquisitionErrorCodes.FixtureDigestMismatch,
                "Live provider bytes do not match the host receipt digest.");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Array.Clear(body);
        body = Array.Empty<byte>();
        disposed = true;
    }
}

public sealed class ProviderResponseTooLargeException : SearchRuleException
{
    public ProviderResponseTooLargeException(IncompleteRuntimeProviderResponseEvidence evidence)
        : base(
            ProviderAcquisitionErrorCodes.ResponseTooLarge,
            "Live provider response exceeds the admitted byte cap.")
    {
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    public IncompleteRuntimeProviderResponseEvidence Evidence { get; }
}

public sealed class ProviderLiveHost : IDisposable
{
    public const int DefaultResponseSizeCap = 8 * 1024 * 1024;
    private static readonly Uri OpenAlexBase = new("https://api.openalex.org");
    private static readonly Uri SemanticScholarBase = new("https://api.semanticscholar.org");
    private readonly IProviderCredentialResolver credentials;
    private readonly IClock clock;
    private readonly HttpClient client;
    private readonly TimeSpan timeout;
    private readonly int responseSizeCap;

    public ProviderLiveHost(
        IProviderCredentialResolver credentials,
        IClock clock,
        TimeSpan? timeout = null,
        int responseSizeCap = DefaultResponseSizeCap,
        HttpMessageHandler? handler = null)
    {
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (this.timeout < TimeSpan.FromSeconds(1) ||
            this.timeout > TimeSpan.FromSeconds(120) ||
            responseSizeCap <= 0 ||
            responseSizeCap > 10 * 1024 * 1024)
        {
            throw new SearchRuleException(
                ProviderAcquisitionErrorCodes.TransportPolicyViolation,
                "Live provider timeout or response-size cap is outside policy.");
        }

        this.responseSizeCap = responseSizeCap;
        client = new HttpClient(handler ?? CreateHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<ProviderLiveHttpResponse> ExecuteAsync(
        ProviderLiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var credential = credentials.Resolve(request.ProviderAlias);
        if (string.IsNullOrWhiteSpace(credential))
        {
            throw new SearchRuleException(
                ProviderAcquisitionErrorCodes.CredentialUnavailable,
                $"A credential is required for provider '{request.ProviderAlias}'.");
        }

        using var message = BuildMessage(request, credential);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var requestedAt = clock.UtcNow;
        try
        {
            using var response = await client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);
            if (response.Headers.Location is not null ||
                (int)response.StatusCode is >= 300 and < 400)
            {
                throw new SearchRuleException(
                    ProviderAcquisitionErrorCodes.TransportPolicyViolation,
                    "Live provider redirects are forbidden.");
            }

            if (response.Content.Headers.ContentEncoding.Any(value =>
                    !string.Equals(value, "identity", StringComparison.OrdinalIgnoreCase)))
            {
                throw new SearchRuleException(
                    ProviderAcquisitionErrorCodes.TransportPolicyViolation,
                    "Encoded live provider response bodies are forbidden.");
            }

            byte[] body;
            try
            {
                body = await ReadBoundedAsync(response.Content, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (ResponseCapExceededException exception)
            {
                var evidence = new IncompleteRuntimeProviderResponseEvidence(
                    request.ProviderAlias,
                    request.Digest,
                    exception.ObservedPrefix,
                    (int)response.StatusCode,
                    requestedAt,
                    clock.UtcNow,
                    ProviderAcquisitionErrorCodes.ResponseTooLarge);
                Array.Clear(exception.ObservedPrefix);
                throw new ProviderResponseTooLargeException(evidence);
            }

            var receivedAt = clock.UtcNow;
            return new ProviderLiveHttpResponse(
                request,
                body,
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                requestedAt,
                receivedAt,
                ObserveHeaders(response));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SearchRuleException(
                ProviderAcquisitionErrorCodes.ProviderTimeout,
                $"Live provider '{request.ProviderAlias}' timed out.");
        }
        catch (HttpRequestException)
        {
            throw new SearchRuleException(
                ProviderAcquisitionErrorCodes.TransportPolicyViolation,
                $"Live provider '{request.ProviderAlias}' request failed.");
        }
    }

    public void Dispose() => client.Dispose();

    private static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false
    };

    private static HttpRequestMessage BuildMessage(ProviderLiveRequest request, string credential)
    {
        var baseUri = request.ProviderAlias switch
        {
            "openalex" => OpenAlexBase,
            "semantic_scholar" => SemanticScholarBase,
            _ => throw new SearchRuleException(
                ProviderAcquisitionErrorCodes.TransportPolicyViolation,
                "Live provider alias is not allowlisted.")
        };
        var endpoint = request.EndpointPathAndQuery;
        if (request.ProviderAlias == "openalex")
        {
            endpoint = $"{endpoint}{(endpoint.Contains('?') ? '&' : '?')}api_key={Uri.EscapeDataString(credential)}";
        }

        var message = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(baseUri, endpoint));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        if (request.ProviderAlias == "semantic_scholar")
        {
            message.Headers.TryAddWithoutValidation("x-api-key", credential);
        }

        if (request.HasBody)
        {
            message.Content = new ByteArrayContent(request.CopySanitizedBody());
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return message;
    }

    private async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 &&
            content.Headers.ContentLength > responseSizeCap)
        {
            throw new ResponseCapExceededException(Array.Empty<byte>());
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(Math.Min(responseSizeCap, 64 * 1024));
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var remainingWithSentinel = checked(responseSizeCap - (int)buffer.Length + 1);
            var read = await stream.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, remainingWithSentinel)),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > responseSizeCap)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                throw new ResponseCapExceededException(buffer.ToArray());
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyDictionary<string, string> ObserveHeaders(HttpResponseMessage response)
    {
        var admitted = new[]
        {
            "content-length",
            "content-type",
            "retry-after",
            "x-rate-limit-limit",
            "x-rate-limit-interval",
            "x-rate-limit-remaining",
            "x-rate-limit-credits-used",
            "x-rate-limit-reset"
        };
        var observed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in admitted)
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                observed[name] = string.Join(",", values);
            }
            else if (response.Content.Headers.TryGetValues(name, out values))
            {
                observed[name] = string.Join(",", values);
            }
        }

        return new ReadOnlyDictionary<string, string>(observed);
    }

    private sealed class ResponseCapExceededException(byte[] observedPrefix) : Exception
    {
        public byte[] ObservedPrefix { get; } = observedPrefix;
    }
}
