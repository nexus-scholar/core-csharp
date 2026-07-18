using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Search;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ProviderSecretPolicyTests
{
    [TestMethod]
    [DataRow("api+key")]
    [DataRow("api%2Bkey")]
    [DataRow("access+key")]
    [DataRow("access%2Bkey")]
    [DataRow("x+api+key")]
    [DataRow("x%2Bapi%2Bkey")]
    public void Provider_secret_policy_rejects_plus_separated_credential_names(string name)
    {
        Assert.IsTrue(ProviderSecretPolicy.ContainsForbiddenDescriptorValue(name, "credential-value"));
    }
}
