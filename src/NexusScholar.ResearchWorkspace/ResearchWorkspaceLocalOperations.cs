using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.ResearchWorkspace;

public enum ResearchWorkspaceOperationStatus
{
    Succeeded,
    Failed,
    Stale,
    RecoveryRequired
}

public sealed record ResearchWorkspaceInitializeRequest(
    string WorkingDirectory,
    string Title,
    string? WorkspaceId,
    DateTimeOffset OccurredAt);

public sealed record ResearchWorkspaceInitializeResult(
    ResearchWorkspaceOperationStatus Status,
    int ExitCode,
    string Message,
    ResearchWorkspaceProject? Project)
{
    public bool Completed => Status == ResearchWorkspaceOperationStatus.Succeeded;
}

public sealed record ResearchWorkspaceSearchImportRequest(
    string WorkingDirectory,
    string SourcePath,
    string Source,
    string Format,
    string? InputId,
    string? Query,
    string ImportedBy,
    DateTimeOffset OccurredAt,
    long? ExpectedProjectRevision = null,
    string? ExpectedSourceDigest = null,
    string? ExpectedWorkspaceId = null);

public sealed record ResearchWorkspaceSearchImportResult(
    ResearchWorkspaceOperationStatus Status,
    int ExitCode,
    string Message,
    ResearchWorkspaceProject? Project,
    string? InputId,
    string? Source,
    string? Format,
    string? RelativeSourcePath,
    string? SourceDigest,
    int ImportedRecordCount,
    int ParserWarningCount,
    int SkippedRecordCount,
    string? TraceRelativePath)
{
    public bool Completed => Status == ResearchWorkspaceOperationStatus.Succeeded;
}

public static class ResearchWorkspaceLocalOperations
{
    private const string ParserId = "nexus.local-workspace.search-import";
    private const string ParserVersion = "1.0.0";

    private static readonly Regex SafeInputId = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ResearchWorkspaceInitializeResult Initialize(ResearchWorkspaceInitializeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var createdDirectories = new Stack<string>();
        string? projectFile = null;
        try
        {
            var root = Path.GetFullPath(Required(request.WorkingDirectory, nameof(request.WorkingDirectory)));
            var title = Required(request.Title, nameof(request.Title));
            projectFile = ResearchWorkspacePaths.ProjectFile(root);
            Directory.CreateDirectory(root);
            using var initializationLock = AcquireInitializationLock(root);
            if (File.Exists(projectFile))
            {
                return InitializeFailure(
                    ResearchWorkspaceOperationStatus.Failed,
                    ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    "A Nexus research workspace already exists in this folder.");
            }

            foreach (var relativeDirectory in ResearchWorkspacePaths.RequiredDirectories)
            {
                var directory = ResearchWorkspacePaths.InProject(root, relativeDirectory);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    createdDirectories.Push(directory);
                }
            }

            var project = ResearchWorkspaceProject.Create(title, request.OccurredAt, request.WorkspaceId);
            ResearchWorkspaceStore.ValidateProject(project);
            ResearchWorkspaceJson.WriteProjectFileAtomic(projectFile, project);
            return new ResearchWorkspaceInitializeResult(
                ResearchWorkspaceOperationStatus.Succeeded,
                ResearchWorkspaceExitCodes.Success,
                "Nexus research workspace initialized",
                project);
        }
        catch (ArgumentException exception)
        {
            CleanupInitialization(projectFile, createdDirectories);
            return InitializeFailure(
                ResearchWorkspaceOperationStatus.Failed,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (JsonException exception)
        {
            CleanupInitialization(projectFile, createdDirectories);
            return InitializeFailure(
                ResearchWorkspaceOperationStatus.Failed,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (ResearchWorkspaceConcurrencyException exception)
        {
            return InitializeFailure(
                ResearchWorkspaceOperationStatus.RecoveryRequired,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CleanupInitialization(projectFile, createdDirectories);
            return InitializeFailure(
                ResearchWorkspaceOperationStatus.RecoveryRequired,
                ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                $"Unable to initialize Nexus research workspace: {exception.Message}");
        }
    }

    public static ResearchWorkspaceSearchImportResult ImportSearch(ResearchWorkspaceSearchImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var workingDirectory = Path.GetFullPath(Required(request.WorkingDirectory, nameof(request.WorkingDirectory)));
            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Failed,
                    ResearchWorkspaceExitCodes.MissingProjectOrInput,
                    "No Nexus research workspace found in the current folder or its parents.");
            }

            var sourcePath = Path.GetFullPath(Required(request.SourcePath, nameof(request.SourcePath)), workingDirectory);
            if (!File.Exists(sourcePath))
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Failed,
                    ResearchWorkspaceExitCodes.MissingProjectOrInput,
                    $"Search export file not found: {DisplayPath(request.SourcePath)}");
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Failed,
                    ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat,
                    $"Unsupported Nexus project schema: {project.Schema}");
            }

            if (request.ExpectedProjectRevision is { } expectedRevision && project.Revision != expectedRevision)
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Stale,
                    ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    $"stale-workspace-revision: expected revision {expectedRevision}, but found {project.Revision}.");
            }

            if (request.ExpectedWorkspaceId is { } expectedWorkspaceId &&
                !string.Equals(project.WorkspaceId, expectedWorkspaceId, StringComparison.Ordinal))
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Stale,
                    ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    "stale-workspace-identity: the selected folder now contains a different workspace.");
            }

            var source = SearchImportAliases.NormalizeSource(Required(request.Source, nameof(request.Source)));
            var format = SearchImportAliases.NormalizeFormat(Required(request.Format, nameof(request.Format)));
            var inputId = string.IsNullOrWhiteSpace(request.InputId) ? NextInputId(project) : request.InputId.Trim();
            ValidateInputId(inputId);

            if (project.Inputs.Any(input => string.Equals(input.EffectiveInputId, inputId, StringComparison.Ordinal)))
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Failed,
                    ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    $"A search export with input id '{inputId}' already exists.");
            }

            var sourceBytes = File.ReadAllBytes(sourcePath);
            var sourceDigest = ContentDigest.Sha256(sourceBytes).ToString();
            if (request.ExpectedSourceDigest is { } expectedDigest &&
                !string.Equals(sourceDigest, expectedDigest, StringComparison.Ordinal))
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Stale,
                    ResearchWorkspaceExitCodes.DigestMismatch,
                    "stale-import-source: the selected Search export changed after preview.");
            }

            var trace = new SearchImportService().Parse(
                $"{inputId}.import-trace",
                new SearchImportRequest(
                    source,
                    SearchImportAliases.ParserFormatFor(format),
                    ParserId,
                    ParserVersion,
                    Required(request.ImportedBy, nameof(request.ImportedBy)),
                    FormatUtc(request.OccurredAt),
                    request.Query),
                sourceBytes);

            var commitSourceBytes = File.ReadAllBytes(sourcePath);
            if (!ContentDigest.Sha256(commitSourceBytes).ToString().Equals(sourceDigest, StringComparison.Ordinal))
            {
                return ImportFailure(
                    ResearchWorkspaceOperationStatus.Stale,
                    ResearchWorkspaceExitCodes.DigestMismatch,
                    "stale-import-source: the selected Search export changed during import preparation.");
            }

            var updatedProject = ResearchWorkspaceTransaction.CommitImport(
                location,
                project,
                new ResearchWorkspaceInput
                {
                    InputId = inputId,
                    Kind = "search-export",
                    Source = source,
                    Format = format,
                    Sha256 = sourceDigest,
                    QueryId = inputId,
                    QueryText = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query.Trim()
                },
                sourceBytes,
                trace,
                SearchImportAliases.ExtensionFor(format));
            var committedInput = updatedProject.Inputs.Single(input =>
                string.Equals(input.EffectiveInputId, inputId, StringComparison.Ordinal));

            return new ResearchWorkspaceSearchImportResult(
                ResearchWorkspaceOperationStatus.Succeeded,
                ResearchWorkspaceExitCodes.Success,
                $"Imported search export: {inputId}",
                updatedProject,
                inputId,
                source,
                format,
                committedInput.EffectiveRelativePath,
                trace.Metadata.SourceFileDigest.ToString(),
                trace.Sightings.Count,
                trace.ParserWarnings.Count,
                trace.ImportedRecords.Count(record => record.IsSkipped),
                committedInput.ImportTracePath);
        }
        catch (ResearchWorkspaceConcurrencyException exception)
        {
            return ImportFailure(
                exception.InnerException is IOException
                    ? ResearchWorkspaceOperationStatus.RecoveryRequired
                    : ResearchWorkspaceOperationStatus.Stale,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (ResearchWorkspaceAuthorityGenerationActiveException exception)
        {
            return ImportFailure(
                ResearchWorkspaceOperationStatus.RecoveryRequired,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (SearchRuleException exception)
        {
            return ImportFailure(
                ResearchWorkspaceOperationStatus.Failed,
                string.Equals(exception.Category, SearchImportErrorCodes.UnsupportedFormat, StringComparison.Ordinal)
                    ? ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat
                    : ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                $"Search import failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            return ImportFailure(
                ResearchWorkspaceOperationStatus.Failed,
                ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat,
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ImportFailure(
                ResearchWorkspaceOperationStatus.Failed,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ImportFailure(
                ResearchWorkspaceOperationStatus.RecoveryRequired,
                ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                $"Unable to import search export: {exception.Message}");
        }
    }

    private static ResearchWorkspaceInitializeResult InitializeFailure(
        ResearchWorkspaceOperationStatus status,
        int exitCode,
        string message) => new(status, exitCode, message, null);

    private static ResearchWorkspaceSearchImportResult ImportFailure(
        ResearchWorkspaceOperationStatus status,
        int exitCode,
        string message) => new(status, exitCode, message, null, null, null, null, null, null, 0, 0, 0, null);

    private static string NextInputId(ResearchWorkspaceProject project)
    {
        var next = project.Inputs.Select(input => input.EffectiveInputId)
            .Select(ParseSearchInputNumber).DefaultIfEmpty(0).Max() + 1;
        return string.Create(CultureInfo.InvariantCulture, $"search-{next:000}");
    }

    private static int ParseSearchInputNumber(string inputId) =>
        inputId.StartsWith("search-", StringComparison.Ordinal) &&
        int.TryParse(inputId["search-".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;

    private static void ValidateInputId(string inputId)
    {
        if (!SafeInputId.IsMatch(inputId) || inputId.Contains("..", StringComparison.Ordinal) ||
            inputId.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            inputId.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Search query id must be a safe local file segment.");
        }
    }

    private static void CleanupInitialization(string? projectFile, Stack<string> createdDirectories)
    {
        if (projectFile is not null && File.Exists(projectFile))
        {
            File.Delete(projectFile);
        }

        while (createdDirectories.TryPop(out var directory))
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

    private static FileStream AcquireInitializationLock(string root)
    {
        try
        {
            return new FileStream(
                Path.Combine(root, ResearchWorkspacePaths.ProjectLockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException exception)
        {
            throw new ResearchWorkspaceConcurrencyException(
                "The workspace is locked by another initialization or mutation.",
                exception);
        }
    }

    private static string Required(string? value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }

    private static string FormatUtc(DateTimeOffset timestamp) =>
        timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string DisplayPath(string inputPath) =>
        Path.IsPathFullyQualified(inputPath) ? Path.GetFileName(inputPath) : inputPath;
}
