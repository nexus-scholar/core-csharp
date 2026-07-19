using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NexusScholar.Architecture.Tests;

[TestClass]
public sealed class RepositoryPolicyTests
{
    private static readonly string[] ForbiddenPackagePrefixes =
    {
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Avalonia",
        "Amazon.",
        "AWSSDK.",
        "Azure.",
        "Google.",
        "OpenAI",
        "Anthropic",
        "Microsoft.SemanticKernel",
        "RestSharp"
    };

    private static readonly (string Label, Regex Pattern)[] ForbiddenCodePatterns =
    {
        ("HttpClient", new Regex(@"\bHttpClient\b", RegexOptions.Compiled)),
        ("HttpRequestMessage", new Regex(@"\bHttpRequestMessage\b", RegexOptions.Compiled)),
        ("WebRequest", new Regex(@"\bWebRequest\b", RegexOptions.Compiled)),
        ("Socket", new Regex(@"\bSocket\b", RegexOptions.Compiled)),
        ("TcpClient", new Regex(@"\bTcpClient\b", RegexOptions.Compiled)),
        ("UdpClient", new Regex(@"\bUdpClient\b", RegexOptions.Compiled)),
        ("OpenAI", new Regex(@"\bOpenAI\b", RegexOptions.Compiled)),
        ("Anthropic", new Regex(@"\bAnthropic\b", RegexOptions.Compiled)),
        ("SemanticKernel", new Regex(@"\bSemanticKernel\b", RegexOptions.Compiled)),
        ("Azure.AI", new Regex(@"\bAzure\.AI\b", RegexOptions.Compiled)),
        ("Azure.Storage", new Regex(@"\bAzure\.Storage\b", RegexOptions.Compiled)),
        ("Google.Cloud", new Regex(@"\bGoogle\.Cloud\b", RegexOptions.Compiled)),
        ("Google.Apis", new Regex(@"\bGoogle\.Apis\b", RegexOptions.Compiled)),
        ("Amazon.S3", new Regex(@"\bAmazon\.S3\b", RegexOptions.Compiled)),
        ("AWSSDK", new Regex(@"\bAWSSDK\b", RegexOptions.Compiled))
    };

    [TestMethod]
    public void Repository_projects_do_not_reference_forbidden_packages()
    {
        var root = FindRepositoryRoot();
        var projectFiles = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Append(Path.Combine(root, "Directory.Packages.props"))
            .ToArray();

        var violations = new List<string>();

        foreach (var file in projectFiles)
        {
            var document = XDocument.Load(file);
            var references = document.Descendants()
                .Where(element => element.Name.LocalName is "PackageReference" or "PackageVersion")
                .Select(element => (Include: (string?)element.Attribute("Include") ?? string.Empty, File: file));

            foreach (var reference in references)
            {
                if (ForbiddenPackagePrefixes.Any(prefix => reference.Include.StartsWith(prefix, StringComparison.Ordinal)) &&
                    !IsAllowedRendererPackageReference(root, reference.File, reference.Include))
                {
                    violations.Add($"{MakeRelative(root, reference.File)} -> {reference.Include}");
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            $"Forbidden package references found:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [TestMethod]
    public void Avalonia_headless_is_scoped_to_desktop_acceptance_tests()
    {
        var root = FindRepositoryRoot();
        var consumers = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Where(path => XDocument.Load(path)
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "PackageReference" &&
                    string.Equals(
                        (string?)element.Attribute("Include"),
                        "Avalonia.Headless",
                        StringComparison.Ordinal)))
            .Select(path => NormalizeRelativePath(MakeRelative(root, path)))
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "tests/NexusScholar.Desktop.Acceptance.Tests/NexusScholar.Desktop.Acceptance.Tests.csproj" },
            consumers);
    }

    [TestMethod]
    public void Repository_source_and_tests_do_not_use_live_call_primitives_or_provider_sdk_symbols()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Where(path => !IsArchitectureGuardFile(root, path))
            .Where(path => !IsAcceptedLiveProviderBoundary(root, path))
            .ToArray();

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var (label, pattern) in ForbiddenCodePatterns)
            {
                if (pattern.IsMatch(content))
                {
                    violations.Add($"{MakeRelative(root, file)} -> {label}");
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            $"Forbidden live-call or provider symbols found:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [TestMethod]
    public void Architecture_guard_file_paths_are_normalized_cross_platform()
    {
        Assert.AreEqual(
            "tests/NexusScholar.Architecture.Tests/RepositoryPolicyTests.cs",
            NormalizeRelativePath(@"tests\NexusScholar.Architecture.Tests\RepositoryPolicyTests.cs"));

        Assert.AreEqual(
            "tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs",
            NormalizeRelativePath("tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs"));
    }

    [TestMethod]
    public void Repository_identity_is_consistent_across_active_delivery_surfaces()
    {
        const string repository = "https://github.com/nexus-scholar-org/core-csharp";
        const string staleRepository = "https://github.com/nexus-scholar/core-csharp";
        const string stalePages = "https://nexus-scholar.github.io/core-csharp";
        var root = FindRepositoryRoot();
        var buildProperties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var repositoryUrl = buildProperties.Descendants("RepositoryUrl").Single().Value;
        var activeFiles = new[]
        {
            "CONTRIBUTING.md",
            "SECURITY.md",
            "scripts/build-release-evidence.ps1",
            "scripts/verify-github-governance.ps1"
        };

        Assert.AreEqual(repository, repositoryUrl);
        foreach (var relativePath in activeFiles)
        {
            var content = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.IsFalse(
                content.Contains(staleRepository, StringComparison.Ordinal),
                $"{relativePath} contains the stale repository owner.");
            Assert.IsFalse(
                content.Contains(stalePages, StringComparison.Ordinal),
                $"{relativePath} contains the stale Pages owner.");
        }

        StringAssert.Contains(
            File.ReadAllText(Path.Combine(root, "scripts", "build-release-evidence.ps1")),
            repository);
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(root, "scripts", "verify-github-governance.ps1")),
            "nexus-scholar-org/core-csharp");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static bool IsAcceptedLiveProviderBoundary(string root, string path)
    {
        var relative = NormalizeRelativePath(Path.GetRelativePath(root, path));
        return relative.StartsWith("src/NexusScholar.Search.Providers.Live/", StringComparison.Ordinal)
            || relative.StartsWith("tests/NexusScholar.Search.Providers.Live.Tests/", StringComparison.Ordinal);
    }

    private static bool IsArchitectureGuardFile(string root, string path)
    {
        var relative = NormalizeRelativePath(MakeRelative(root, path));

        return string.Equals(
                relative,
                "tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                relative,
                "tests/NexusScholar.Architecture.Tests/RepositoryPolicyTests.cs",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuildArtifact(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedRendererPackageReference(string root, string file, string include)
    {
        if (!include.StartsWith("Avalonia", StringComparison.Ordinal))
        {
            return false;
        }

        var relative = NormalizeRelativePath(MakeRelative(root, file));
        return string.Equals(relative, "Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                relative,
                "src/NexusScholar.Avalonia.Blocks/NexusScholar.Avalonia.Blocks.csproj",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                relative,
                "samples/NexusScholar.Avalonia.Blocks.SampleHost/NexusScholar.Avalonia.Blocks.SampleHost.csproj",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                relative,
                "samples/NexusScholar.Desktop.Preview/NexusScholar.Desktop.Preview.csproj",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                relative,
                "src/NexusScholar.Desktop/NexusScholar.Desktop.csproj",
                StringComparison.OrdinalIgnoreCase)
            || (string.Equals(include, "Avalonia.Headless", StringComparison.Ordinal)
                && string.Equals(
                    relative,
                    "tests/NexusScholar.Desktop.Acceptance.Tests/NexusScholar.Desktop.Acceptance.Tests.csproj",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string MakeRelative(string root, string path)
    {
        return Path.GetRelativePath(root, path);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
