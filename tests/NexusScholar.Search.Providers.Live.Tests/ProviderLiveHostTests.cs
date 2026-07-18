using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.Search.Providers.Live.Tests;

[TestClass]
public sealed class ProviderLiveHostTests
{
    [TestMethod]
    public async Task OpenAlex_credential_is_injected_only_at_send_time()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"meta":{},"results":[]}"""));
        var plan = LiveGet(
            "openalex.works",
            "openalex",
            "/works?search=tomato&per_page=1&cursor=%2A");
        using var host = Host(handler, "openalex");
        using var response = await host.ExecuteAsync(plan);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsFalse(plan.EndpointPathAndQuery.Contains("test-credential", StringComparison.Ordinal));
        Assert.IsFalse(plan.Envelope().ToString()!.Contains("test-credential", StringComparison.Ordinal));
        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual("api.openalex.org", handler.LastRequest.RequestUri!.Host);
        Assert.IsTrue(handler.LastRequest.RequestUri.Query.Contains("api_key=test-credential", StringComparison.Ordinal));
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task SemanticScholar_credential_is_a_header_and_batch_body_is_preserved()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var body = Encoding.UTF8.GetBytes("""{"ids":["DOI:10.1/example"]}""");
        var plan = LivePost(
            "semantic-scholar.paper-batch",
            "semantic_scholar",
            "/graph/v1/paper/batch?fields=paperId%2Ctitle",
            body);
        using var host = Host(handler, "semantic_scholar");
        using var response = await host.ExecuteAsync(plan);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(handler.LastRequest!.Headers.TryGetValues("x-api-key", out var values));
        Assert.AreEqual("test-credential", values.Single());
        Assert.IsFalse(handler.LastRequest.RequestUri!.Query.Contains("key", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task Missing_credential_is_rejected_before_network()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("must not send"));
        var plan = LiveGet(
            "openalex.works",
            "openalex",
            "/works?search=tomato&per_page=1&cursor=%2A");
        using var host = new ProviderLiveHost(
            new FixedCredentialResolver(null),
            new SequenceClock(),
            handler: handler);

        var exception = await Assert.ThrowsExactlyAsync<SearchRuleException>(() => host.ExecuteAsync(plan));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.CredentialUnavailable, exception.Category);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public void Wrong_operation_path_method_and_secret_descriptor_are_rejected()
    {
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "openalex", "https://api.openalex.org/works"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "openalex", "/other"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "semantic_scholar", "/works"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "openalex", "/works?api_key=secret"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "openalex", "/works?search=api_key%3Dsecret"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "openalex", "/works?search=mailto%3Aoperator%40example.test"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LiveGet("openalex.works", "openalex", "/works?search=https%3A%2F%2Fexample.test"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LivePost(
                "semantic-scholar.paper-batch",
                "semantic_scholar",
                "/graph/v1/paper/batch",
                Encoding.UTF8.GetBytes("""{"ids":[],"secret":"value"}""")));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            LivePost(
                "semantic-scholar.paper-batch",
                "semantic_scholar",
                "/graph/v1/paper/batch",
                Encoding.UTF8.GetBytes("""{"ids":["sk-secret"]}""")));
    }

    [TestMethod]
    public async Task Redirect_and_encoded_response_are_rejected_without_retry()
    {
        foreach (var response in new[]
        {
            Redirect("https://api.openalex.org/works"),
            Encoded()
        })
        {
            var handler = new RecordingHandler(_ => response);
            using var host = Host(handler, "openalex");
            var exception = await Assert.ThrowsExactlyAsync<SearchRuleException>(() =>
                host.ExecuteAsync(LiveGet(
                    "openalex.works",
                    "openalex",
                    "/works?search=tomato&per_page=1&cursor=%2A")));
            Assert.AreEqual(ProviderAcquisitionErrorCodes.TransportPolicyViolation, exception.Category);
            Assert.AreEqual(1, handler.CallCount);
        }
    }

    [TestMethod]
    public async Task Oversized_body_is_rejected_without_retry()
    {
        foreach (var (response, expectedPrefixLength) in new[]
        {
            (Json(HttpStatusCode.OK, "123456"), 0L),
            (Streaming("123456"), 6L)
        })
        {
            var handler = new RecordingHandler(_ => response);
            using var host = new ProviderLiveHost(
                new FixedCredentialResolver("test-credential"),
                new SequenceClock(),
                responseSizeCap: 5,
                handler: handler);

            var exception = await Assert.ThrowsExactlyAsync<ProviderResponseTooLargeException>(() =>
                host.ExecuteAsync(LiveGet(
                    "openalex.works",
                    "openalex",
                    "/works?search=tomato&per_page=1&cursor=%2A")));
            Assert.AreEqual(ProviderAcquisitionErrorCodes.ResponseTooLarge, exception.Category);
            Assert.IsFalse(exception.Evidence.BodyComplete);
            Assert.AreEqual(expectedPrefixLength, exception.Evidence.ObservedPrefixLength);
            Assert.AreEqual(ProviderAcquisitionErrorCodes.ResponseTooLarge, exception.Evidence.StopReason);
            Assert.AreEqual(1, handler.CallCount);
        }
    }

    [TestMethod]
    public async Task Timeout_is_terminal_and_not_retried()
    {
        var handler = new DelayedHandler();
        using var host = new ProviderLiveHost(
            new FixedCredentialResolver("test-credential"),
            new SequenceClock(),
            timeout: TimeSpan.FromSeconds(1),
            handler: handler);

        var exception = await Assert.ThrowsExactlyAsync<SearchRuleException>(() =>
            host.ExecuteAsync(LiveGet(
                "openalex.works",
                "openalex",
                "/works?search=tomato&per_page=1&cursor=%2A")));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderTimeout, exception.Category);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public void Runtime_evidence_binds_exact_bytes_and_rejects_unadmitted_headers()
    {
        var body = Encoding.UTF8.GetBytes("""{"data":[]}""");
        var evidence = RuntimeProviderResponseEvidence.Capture(
            "semantic_scholar",
            ContentDigest.Sha256(Encoding.UTF8.GetBytes("request")),
            "parser",
            "1.0.0",
            body,
            200,
            "application/json",
            DateTimeOffset.Parse("2026-07-17T10:00:00Z"),
            DateTimeOffset.Parse("2026-07-17T10:00:01Z"),
            new Dictionary<string, string> { ["x-rate-limit-remaining"] = "2" });

        evidence.Verify(body);
        var mutated = body.ToArray();
        mutated[^2] ^= 1;
        Assert.ThrowsExactly<SearchRuleException>(() => evidence.Verify(mutated));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            RuntimeProviderResponseEvidence.Capture(
                "semantic_scholar",
                ContentDigest.Sha256(Encoding.UTF8.GetBytes("request")),
                "parser",
                "1.0.0",
                body,
                200,
                "application/json",
                DateTimeOffset.Parse("2026-07-17T10:00:00Z"),
                DateTimeOffset.Parse("2026-07-17T10:00:01Z"),
                new Dictionary<string, string> { ["authorization"] = "secret" }));
    }

    [TestMethod]
    public async Task Disposed_response_clears_transient_body()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "sensitive response"));
        using var host = Host(handler, "openalex");
        var response = await host.ExecuteAsync(LiveGet(
            "openalex.works",
            "openalex",
            "/works?search=tomato&per_page=1&cursor=%2A"));
        var bytes = response.CopyBody();
        var mutated = bytes.ToArray();
        mutated[0] ^= 1;
        Assert.ThrowsExactly<SearchRuleException>(() => response.VerifyReceipt(mutated));
        response.VerifyReceipt(bytes);
        response.Dispose();
        Assert.IsFalse(response.HasBody);
        Assert.ThrowsExactly<ObjectDisposedException>(() => response.CopyBody());
        Assert.IsTrue(bytes.Any(value => value != 0));
    }

    [TestMethod]
    public void ProviderLiveRequest_factory_methods_are_not_public()
    {
        var type = typeof(ProviderLiveRequest);
        var publicGet = type.GetMethod(
            "Get",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string), typeof(string) },
            null);
        Assert.IsNull(publicGet, "ProviderLiveRequest.Get must not be public.");

        var publicPost = type.GetMethod(
            "Post",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string), typeof(string), typeof(byte[]) },
            null);
        Assert.IsNull(publicPost, "ProviderLiveRequest.Post must not be public.");

        var publicConstructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.AreEqual(0, publicConstructors.Length, "ProviderLiveRequest must not be directly constructible.");

        var internalGet = type.GetMethod(
            "Get",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string), typeof(string) },
            null);
        Assert.IsNotNull(internalGet);
        Assert.IsTrue(internalGet!.IsAssembly, "ProviderLiveRequest.Get should be internal.");

        var internalPost = type.GetMethod(
            "Post",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string), typeof(string), typeof(byte[]) },
            null);
        Assert.IsNotNull(internalPost);
        Assert.IsTrue(internalPost!.IsAssembly, "ProviderLiveRequest.Post should be internal.");
    }

    private static ProviderLiveRequest LiveGet(string operationId, string providerAlias, string endpointPathAndQuery)
        => ProviderLiveRequest.Get(operationId, providerAlias, endpointPathAndQuery);

    private static ProviderLiveRequest LivePost(
        string operationId,
        string providerAlias,
        string endpointPathAndQuery,
        byte[] body)
        => ProviderLiveRequest.Post(operationId, providerAlias, endpointPathAndQuery, body);

    private static ProviderLiveHost Host(RecordingHandler handler, string providerAlias) =>
        new(
            new FixedCredentialResolver(providerAlias is "openalex" or "semantic_scholar"
                ? "test-credential"
                : null),
            new SequenceClock(),
            handler: handler);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Streaming(string body) => new(HttpStatusCode.OK)
    {
        Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(body))
    };

    private static HttpResponseMessage Redirect(string location)
    {
        var response = Json(HttpStatusCode.Redirect, "{}");
        response.Headers.Location = new Uri(location);
        return response;
    }

    private static HttpResponseMessage Encoded()
    {
        var response = Json(HttpStatusCode.OK, "{}");
        response.Content.Headers.ContentEncoding.Add("gzip");
        return response;
    }

    private sealed class FixedCredentialResolver(string? credential) : IProviderCredentialResolver
    {
        public string? Resolve(string providerAlias) => credential;
    }

    private sealed class SequenceClock : IClock
    {
        private int ticks;
        public DateTimeOffset UtcNow =>
            DateTimeOffset.Parse("2026-07-17T10:00:00Z").AddSeconds(ticks++);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(response(request));
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Json(HttpStatusCode.OK, "{}");
        }
    }

    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

}
