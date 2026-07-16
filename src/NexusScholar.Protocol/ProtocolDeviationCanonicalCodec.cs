using NexusScholar.Kernel;

namespace NexusScholar.Protocol;

public static class ProtocolDeviationCanonicalCodec
{
    public static byte[] Serialize(VerifiedProtocolDeviation deviation) => CanonicalJsonSerializer.SerializeToUtf8Bytes(
        new DigestEnvelope(DigestScope.CanonicalJsonRecord, ProtocolDeviationConstants.SchemaId,
            ProtocolDeviationConstants.SchemaVersion, deviation.Deviation.ToCanonicalJson()).ToCanonicalJsonObject());

    public static VerifiedProtocolDeviation Rehydrate(byte[] bytes, ContentDigest expectedDigest,
        UnverifiedProtocolDeviation input, IProtocolDeviationAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var verified = ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(input, resolver);
        if (verified.DeviationDigest != expectedDigest || !bytes.SequenceEqual(Serialize(verified)))
            throw new ProtocolRuleException(ProtocolErrorCodes.InvalidDeviation,
                "Deviation record must use exact canonical bytes reconstructed from verified authority.");
        return verified;
    }
}
