using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class KernelFixtureTests
{
    [TestMethod]
    public void Kernel_canonical_digest_fixture_matches_expected_output()
    {
        using var document = LoadFixture("kernel-canonical-digest.json");
        var root = document.RootElement;

        AssertHasGeneratorMetadata(root, "kernel-canonical-digest-v1");
        var envelopeRoot = root.GetProperty("envelope");
        DigestEnvelope.ValidateCanonicalShape(envelopeRoot);

        var content = (CanonicalJsonObject)CanonicalJsonValue.FromJsonElement(envelopeRoot.GetProperty("content"));
        var envelope = new DigestEnvelope(
            DigestScope.Parse(envelopeRoot.GetProperty("scope").GetString()!),
            envelopeRoot.GetProperty("schema").GetString()!,
            envelopeRoot.GetProperty("schemaVersion").GetString()!,
            content);

        var canonicalJson = envelope.ToCanonicalJson();
        var digest = envelope.ComputeDigest();

        Assert.AreEqual(root.GetProperty("expectedCanonicalJson").GetString(), canonicalJson);
        Assert.AreEqual(root.GetProperty("outputDigest").GetString(), digest.ToString());
        Assert.AreEqual(root.GetProperty("inputDigest").GetString(), digest.ToString());
    }

    [TestMethod]
    public void Kernel_rfc8785_number_vectors_match_expected_output()
    {
        using var document = LoadFixture("kernel-rfc8785-number-vectors.json");
        var root = document.RootElement;

        AssertHasGeneratorMetadata(root, "kernel-rfc8785-number-vectors-v1");

        foreach (var vector in root.GetProperty("vectors").EnumerateArray())
        {
            CanonicalJsonValue value = vector.GetProperty("inputMode").GetString() switch
            {
                "bit-pattern" => CanonicalJsonValue.From(DoubleFromBitPattern(vector.GetProperty("inputHex").GetString()!)),
                _ => throw new InvalidOperationException(
                    $"Unknown inputMode for vector '{vector.GetProperty("label").GetString()}'.")
            };

            Assert.AreEqual(
                vector.GetProperty("expectedCanonical").GetString(),
                CanonicalJsonSerializer.Serialize(value),
                vector.GetProperty("label").GetString());
        }
    }

    [TestMethod]
    public void Kernel_rfc8785_number_vectors_reject_non_finite_bit_patterns()
    {
        using var document = LoadFixture("kernel-rfc8785-number-vectors-invalid.json");
        var root = document.RootElement;

        AssertHasGeneratorMetadata(root, "kernel-rfc8785-number-vectors-invalid-v1");

        foreach (var vector in root.GetProperty("vectors").EnumerateArray())
        {
            if (vector.GetProperty("inputMode").GetString() != "bit-pattern")
            {
                throw new InvalidOperationException(
                    $"Unknown inputMode for vector '{vector.GetProperty("label").GetString()}'.");
            }

            var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                CanonicalJsonValue.From(DoubleFromBitPattern(vector.GetProperty("inputHex").GetString()!)));
            StringAssert.Contains(
                exception.Message,
                vector.GetProperty("expectedErrorContains").GetString()!,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public void Kernel_raw_bytes_digest_fixture_matches_expected_output()
    {
        using var document = LoadFixture("kernel-raw-bytes-digest.json");
        var root = document.RootElement;

        AssertHasGeneratorMetadata(root, "kernel-raw-bytes-digest-v1");

        var bytes = Convert.FromHexString(root.GetProperty("inputHex").GetString()!);
        var digest = ContentDigest.Sha256(bytes);

        Assert.AreEqual(root.GetProperty("outputDigest").GetString(), digest.ToString());
        Assert.AreEqual(root.GetProperty("inputDigest").GetString(), digest.ToString());
    }

    [TestMethod]
    public void Kernel_ndjson_fixture_matches_expected_output()
    {
        using var document = LoadFixture("kernel-ndjson-lf.json");
        var root = document.RootElement;

        AssertHasGeneratorMetadata(root, "kernel-ndjson-lf-v1");

        var ndjson = Encoding.UTF8.GetString(LoadFixtureBytes(root.GetProperty("path").GetString()!));
        var canonical = NdjsonCanonicalizer.Canonicalize(ndjson);
        var digest = ContentDigest.Sha256(Encoding.UTF8.GetBytes(canonical));

        Assert.AreEqual(root.GetProperty("normalizedNdjson").GetString(), canonical);
        Assert.AreEqual(root.GetProperty("outputDigest").GetString(), digest.ToString());
        Assert.AreEqual(root.GetProperty("inputDigest").GetString(), digest.ToString());
    }

    [TestMethod]
    public void Kernel_negative_digest_fixtures_are_rejected()
    {
        AssertFixtureThrowsFormat(
            "kernel-invalid-digest-uppercase.json",
            "kernel-invalid-digest-uppercase-v1",
            root => ContentDigest.Parse(root.GetProperty("digest").GetString()!));
        AssertFixtureThrowsFormat(
            "kernel-invalid-digest-missing-prefix.json",
            "kernel-invalid-digest-missing-prefix-v1",
            root => ContentDigest.Parse(root.GetProperty("digest").GetString()!));
    }

    [TestMethod]
    public void Kernel_negative_validation_fixtures_are_rejected()
    {
        AssertFixtureThrowsInvalidOperation(
            "kernel-invalid-envelope-missing-fields.json",
            "kernel-invalid-envelope-missing-fields-v1",
            root => DigestEnvelope.ValidateCanonicalShape(root.GetProperty("envelope")));
        AssertFixtureThrowsInvalidOperation(
            "kernel-invalid-nfc.json",
            "kernel-invalid-nfc-v1",
            root => CanonicalJsonSerializer.Serialize(
                new CanonicalJsonObject().Add(
                    root.GetProperty("propertyName").GetString()!,
                    root.GetProperty("propertyValue").GetString()!),
                new CanonicalJsonSerializerOptions
                {
                    StringNormalization = CanonicalStringNormalizationMode.RequireNormalized
                }));
        AssertFixtureThrowsFormat(
            "kernel-invalid-timestamp.json",
            "kernel-invalid-timestamp-v1",
            root => CanonicalTimestamp.ValidateCanonicalUtc(root.GetProperty("timestamp").GetString()!));
        AssertFixtureThrowsArgumentOutOfRange(
            "kernel-invalid-number.json",
            "kernel-invalid-number-v1",
            root =>
            {
                var kind = root.GetProperty("numberKind").GetString();
                _ = kind switch
                {
                    "NaN" => CanonicalJsonValue.From(double.NaN),
                    "Infinity" => CanonicalJsonValue.From(double.PositiveInfinity),
                    "-Infinity" => CanonicalJsonValue.From(double.NegativeInfinity),
                    _ => throw new InvalidOperationException("Unknown non-finite test vector.")
                };
            });
        AssertFixtureThrowsArgumentOutOfRange(
            "kernel-invalid-number-parse.json",
            "kernel-invalid-number-parse-v1",
            root => ParseJsonNumber(root.GetProperty("inputText").GetString()!));
    }

    [TestMethod]
    public void Kernel_negative_ndjson_fixtures_are_rejected()
    {
        AssertFixtureThrowsInvalidOperation(
            "kernel-ndjson-crlf-reject.json",
            "kernel-ndjson-crlf-reject-v1",
            root => NdjsonCanonicalizer.Canonicalize(
                Encoding.UTF8.GetString(LoadFixtureBytes(root.GetProperty("path").GetString()!))));
        AssertFixtureThrowsInvalidOperation(
            "kernel-ndjson-bom-reject.json",
            "kernel-ndjson-bom-reject-v1",
            root => NdjsonCanonicalizer.Canonicalize(
                Encoding.UTF8.GetString(LoadFixtureBytes(root.GetProperty("path").GetString()!))));
    }

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "kernel", fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static CanonicalJsonValue ParseJsonNumber(string text)
    {
        using var document = JsonDocument.Parse($"[{text}]");
        return CanonicalJsonValue.FromJsonElement(document.RootElement[0]);
    }

    private static byte[] LoadFixtureBytes(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "kernel", fileName);
        return File.ReadAllBytes(path);
    }

    private static double DoubleFromBitPattern(string inputHex)
    {
        var bits = Convert.ToUInt64(inputHex, 16);
        return BitConverter.Int64BitsToDouble(unchecked((long)bits));
    }

    private static void AssertHasGeneratorMetadata(JsonElement root, string fixtureId)
    {
        Assert.AreEqual(fixtureId, root.GetProperty("fixtureId").GetString());
        Assert.IsTrue(root.GetProperty("sourceRefs").GetArrayLength() >= 2);
        Assert.IsTrue(root.GetProperty("sourceCommit").GetString()?.Length > 0);
        Assert.IsTrue(root.GetProperty("generatorCommand").GetString()?.Length > 0);
        Assert.IsTrue(root.GetProperty("generatorVersion").GetString()?.Length > 0);
    }

    private static void AssertFixtureThrowsFormat(string fileName, string fixtureId, Action<JsonElement> action)
    {
        using var document = LoadFixture(fileName);
        var root = document.RootElement;
        AssertHasGeneratorMetadata(root, fixtureId);

        var exception = Assert.ThrowsExactly<FormatException>(() => action(root));
        StringAssert.Contains(exception.Message, root.GetProperty("expectedErrorContains").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertFixtureThrowsInvalidOperation(string fileName, string fixtureId, Action<JsonElement> action)
    {
        using var document = LoadFixture(fileName);
        var root = document.RootElement;
        AssertHasGeneratorMetadata(root, fixtureId);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => action(root));
        StringAssert.Contains(exception.Message, root.GetProperty("expectedErrorContains").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertFixtureThrowsArgumentOutOfRange(string fileName, string fixtureId, Action<JsonElement> action)
    {
        using var document = LoadFixture(fileName);
        var root = document.RootElement;
        AssertHasGeneratorMetadata(root, fixtureId);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => action(root));
        StringAssert.Contains(exception.Message, root.GetProperty("expectedErrorContains").GetString()!, StringComparison.OrdinalIgnoreCase);
    }
}
