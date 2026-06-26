using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Artifacts;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class DeterministicKernelTests
{
    [TestMethod]
    public void Content_digest_renders_canonical_sha256_lowercase_hex()
    {
        var digest = ContentDigest.Parse("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

        Assert.AreEqual("sha256", digest.Algorithm.Value);
        Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", digest.Value);
        Assert.AreEqual("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", digest.ToString());
    }

    [TestMethod]
    public void Content_digest_rejects_uppercase_hex()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            ContentDigest.Parse("sha256:BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"));
    }

    [TestMethod]
    public void Content_digest_rejects_missing_algorithm_prefix()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            ContentDigest.Parse("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }

    [TestMethod]
    public void Content_digest_rejects_wrong_length()
    {
        Assert.ThrowsExactly<FormatException>(() => ContentDigest.Parse("sha256:abcd"));
    }

    [TestMethod]
    public void Raw_byte_sha256_digest_matches_known_vector()
    {
        var digest = ContentDigest.Sha256(Encoding.UTF8.GetBytes("abc"));

        Assert.AreEqual(
            "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            digest.ToString());
    }

    [TestMethod]
    public void Canonical_json_orders_properties_and_preserves_nested_arrays()
    {
        var value = CanonicalJsonValue.Object(
            ("zeta", CanonicalJsonValue.From(2)),
            ("alpha", CanonicalJsonValue.Object(
                ("beta", CanonicalJsonValue.Array(
                    CanonicalJsonValue.From(3),
                    CanonicalJsonValue.From(1),
                    CanonicalJsonValue.From(2))),
                ("alpha", CanonicalJsonValue.From(true)))));

        var json = CanonicalJsonSerializer.Serialize(value);

        Assert.AreEqual(
            "{\"alpha\":{\"alpha\":true,\"beta\":[3,1,2]},\"zeta\":2}",
            json);
    }

    [TestMethod]
    public void Canonical_json_uses_rfc8785_utf16_property_sort_order()
    {
        var value = new CanonicalJsonObject()
            .Add("\u20ac", "Euro Sign")
            .Add("\r", "Carriage Return")
            .Add("\ufb33", "Hebrew Letter Dalet With Dagesh")
            .Add("1", "One")
            .Add("\ud83d\ude00", "Emoji: Grinning Face")
            .Add("\u0080", "Control")
            .Add("\u00f6", "Latin Small Letter O With Diaeresis");

        var json = CanonicalJsonSerializer.Serialize(value);

        Assert.AreEqual(
            "{\"\\r\":\"Carriage Return\",\"1\":\"One\",\"\":\"Control\",\"ö\":\"Latin Small Letter O With Diaeresis\",\"דּ\":\"Hebrew Letter Dalet With Dagesh\",\"€\":\"Euro Sign\",\"😀\":\"Emoji: Grinning Face\"}",
            json);
    }

    [TestMethod]
    public void Canonical_json_preserves_null_versus_omission()
    {
        var withNull = new CanonicalJsonObject()
            .Add("status", "known")
            .AddNull("note");
        var omitted = new CanonicalJsonObject()
            .Add("status", "known");

        Assert.AreEqual("{\"note\":null,\"status\":\"known\"}", CanonicalJsonSerializer.Serialize(withNull));
        Assert.AreEqual("{\"status\":\"known\"}", CanonicalJsonSerializer.Serialize(omitted));
    }

    [TestMethod]
    public void Canonical_json_distinguishes_null_and_omission_in_digest_material()
    {
        var withNull = ContentDigest.Sha256CanonicalJson(new CanonicalJsonObject().Add("status", "known").AddNull("note"));
        var omitted = ContentDigest.Sha256CanonicalJson(new CanonicalJsonObject().Add("status", "known"));

        Assert.AreNotEqual(withNull, omitted);
    }

    [TestMethod]
    public void Canonical_timestamp_emits_utc_fixed_precision()
    {
        var timestamp = CanonicalTimestamp.FormatUtc(new DateTimeOffset(2026, 6, 26, 11, 15, 30, 123, TimeSpan.FromHours(1)).AddTicks(4567));

        Assert.AreEqual("2026-06-26T10:15:30.1234567Z", timestamp);
    }

    [TestMethod]
    public void Canonical_timestamp_validator_rejects_noncanonical_forms()
    {
        Assert.IsTrue(CanonicalTimestamp.IsCanonicalUtc("2026-06-26T10:15:30.1234567Z"));
        Assert.IsFalse(CanonicalTimestamp.IsCanonicalUtc("2026-06-26T10:15:30Z"));
        Assert.IsFalse(CanonicalTimestamp.IsCanonicalUtc("2026-06-26T10:15:30.1234567+01:00"));
        Assert.IsFalse(CanonicalTimestamp.IsCanonicalUtc("2026-06-26T10:15:30.1234567z"));
    }

    [TestMethod]
    public void Canonical_json_normalizes_unicode_to_nfc()
    {
        var decomposed = new CanonicalJsonObject().Add("title", "Re\u0301sume\u0301");
        var composed = new CanonicalJsonObject().Add("title", "Résumé");

        Assert.AreEqual(
            CanonicalJsonSerializer.Serialize(composed),
            CanonicalJsonSerializer.Serialize(decomposed));
    }

    [TestMethod]
    public void Canonical_json_validation_mode_rejects_non_nfc_strings()
    {
        var value = new CanonicalJsonObject().Add("title", "Re\u0301sume\u0301");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CanonicalJsonSerializer.Serialize(
                value,
                new CanonicalJsonSerializerOptions
                {
                    StringNormalization = CanonicalStringNormalizationMode.RequireNormalized
                }));
    }

    [TestMethod]
    public void Canonical_json_validation_mode_rejects_non_nfc_property_names()
    {
        var value = new CanonicalJsonObject().Add("Re\u0301sume\u0301", "title");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CanonicalJsonSerializer.Serialize(
                value,
                new CanonicalJsonSerializerOptions
                {
                    StringNormalization = CanonicalStringNormalizationMode.RequireNormalized
                }));
    }

    [TestMethod]
    public void Canonical_json_rejects_property_name_collisions_after_normalization()
    {
        var value = new CanonicalJsonObject()
            .Add("Résumé", "composed")
            .Add("Re\u0301sume\u0301", "decomposed");

        Assert.ThrowsExactly<InvalidOperationException>(() => CanonicalJsonSerializer.Serialize(value));
    }

    [TestMethod]
    public void Canonical_json_rejects_nan_and_infinities()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CanonicalJsonValue.From(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CanonicalJsonValue.From(double.PositiveInfinity));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CanonicalJsonValue.From(double.NegativeInfinity));
    }

    [TestMethod]
    public void Canonical_json_uses_jcs_number_rendering_for_gate_two_vectors()
    {
        var numbers = CanonicalJsonValue.Array(
            CanonicalJsonValue.From(333333333.33333329d),
            CanonicalJsonValue.From(1e30d),
            CanonicalJsonValue.From(4.50d),
            CanonicalJsonValue.From(2e-3d),
            CanonicalJsonValue.From(0.000000000000000000000000001d));

        Assert.AreEqual("[333333333.3333333,1e+30,4.5,0.002,1e-27]", CanonicalJsonSerializer.Serialize(numbers));
    }

    [TestMethod]
    public void Canonical_json_canonicalizes_decimal_exponents()
    {
        var numbers = CanonicalJsonValue.Array(
            CanonicalJsonValue.From(0.0000001m),
            CanonicalJsonValue.From(10000000000000000000000000000m));

        Assert.AreEqual("[1e-7,10000000000000000000000000000]", CanonicalJsonSerializer.Serialize(numbers));
    }

    [TestMethod]
    public void Ndjson_digest_rejects_crlf_by_default()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ContentDigest.Sha256NdjsonUtf8("{\"id\":1}\r\n{\"id\":2}\r\n"));
    }

    [TestMethod]
    public void Ndjson_digest_can_normalize_crlf_when_explicitly_enabled()
    {
        var lfDigest = ContentDigest.Sha256NdjsonUtf8("{\"id\":1}\n{\"id\":2}\n");
        var normalizedDigest = ContentDigest.Sha256NdjsonUtf8(
            "{\"id\":1}\r\n{\"id\":2}\r\n",
            new NdjsonCanonicalizerOptions
            {
                NormalizeCrLfToLf = true
            });

        Assert.AreEqual(lfDigest, normalizedDigest);
    }

    [TestMethod]
    public void Digest_envelope_canonical_json_and_digest_are_stable_across_runs()
    {
        var content = new CanonicalJsonObject()
            .Add("actors", CanonicalJsonValue.Array(
                CanonicalJsonValue.From("researcher-1"),
                CanonicalJsonValue.From("reviewer-2")))
            .Add("note", "Résumé")
            .AddNull("optional")
            .AddTimestamp("timestamp", new DateTimeOffset(2026, 6, 26, 10, 15, 30, 123, TimeSpan.Zero).AddTicks(4567));
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            "nexus.kernel.fixture",
            "1.0.0",
            content);

        var firstJson = envelope.ToCanonicalJson();
        var secondJson = envelope.ToCanonicalJson();
        var firstDigest = envelope.ComputeDigest();
        var secondDigest = envelope.ComputeDigest();

        Assert.AreEqual(secondJson, firstJson);
        Assert.AreEqual(secondDigest, firstDigest);
        Assert.AreEqual(
            "{\"algorithm\":\"sha256\",\"canonicalizationProfile\":\"rfc8785-jcs\",\"content\":{\"actors\":[\"researcher-1\",\"reviewer-2\"],\"note\":\"Résumé\",\"optional\":null,\"timestamp\":\"2026-06-26T10:15:30.1234567Z\"},\"schema\":\"nexus.kernel.fixture\",\"schemaVersion\":\"1.0.0\",\"scope\":\"canonical-json-record\"}",
            firstJson);
    }

    [TestMethod]
    public void Digest_envelope_freezes_canonical_content_after_construction()
    {
        var sourceContent = new CanonicalJsonObject().Add("status", "known");
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            "nexus.kernel.fixture",
            "1.0.0",
            sourceContent);

        sourceContent.Add("later", "mutation");
        Assert.ThrowsExactly<InvalidOperationException>(() => envelope.Content.Add("forbidden", "mutation"));

        Assert.AreEqual(
            "{\"algorithm\":\"sha256\",\"canonicalizationProfile\":\"rfc8785-jcs\",\"content\":{\"status\":\"known\"},\"schema\":\"nexus.kernel.fixture\",\"schemaVersion\":\"1.0.0\",\"scope\":\"canonical-json-record\"}",
            envelope.ToCanonicalJson());
    }
}
