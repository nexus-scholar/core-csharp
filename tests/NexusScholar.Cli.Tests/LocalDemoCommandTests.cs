using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class LocalDemoCommandTests
{
    [TestMethod]
    public void Demo_output_contains_stable_required_lines()
    {
        var output = LocalDemoCommand.FormatOutput();

        foreach (var line in LocalDemoCommand.RequiredOutputLines)
        {
            StringAssert.Contains(output, line);
        }
    }

    [TestMethod]
    public void Demo_output_is_deterministic_across_repeated_invocations()
    {
        var first = LocalDemoCommand.FormatOutput();
        var second = LocalDemoCommand.FormatOutput();

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Help_text_includes_demo()
    {
        StringAssert.Contains(CliApplication.Usage, "demo");
    }

    [TestMethod]
    public void Unknown_command_returns_non_zero_and_prints_usage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CliApplication.Run(new[] { "unknown" }, output, error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), CliApplication.Usage);
    }

    [TestMethod]
    public void Demo_output_preserves_non_claim_text()
    {
        var output = LocalDemoCommand.FormatOutput();

        StringAssert.Contains(output, "no live providers");
        StringAssert.Contains(output, "no provider SDKs");
        StringAssert.Contains(output, "no persistence/API/cloud");
        StringAssert.Contains(output, "no PDF/OCR");
        StringAssert.Contains(output, "no PHP compatibility");
    }
}
