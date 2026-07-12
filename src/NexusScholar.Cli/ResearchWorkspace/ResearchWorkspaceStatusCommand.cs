using System.Text.Json;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceStatusCommand
{
    public static int Run(TextWriter output, TextWriter error, string workingDirectory)
    {
        try
        {
            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                error.WriteLine("No Nexus research workspace found in the current folder or its parents.");
                error.WriteLine("Run: nexus init --title \"<research title>\"");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
            {
                error.WriteLine($"Unsupported Nexus project schema: {project.Schema}");
                return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
            }

            var report = ResearchWorkspaceVerifier.Verify(location, project);
            var snapshot = WorkspaceStatusSnapshot.Create(location, project, report, workingDirectory);
            WriteSnapshot(output, project, snapshot);
            return snapshot.ExitCode;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"Malformed Nexus project file or generated workspace output: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
        }
        catch (SearchRuleException exception)
        {
            error.WriteLine($"Unable to inspect imported search evidence: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to read Nexus research workspace: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static void WriteSnapshot(
        TextWriter output,
        ResearchWorkspaceProject project,
        WorkspaceStatusSnapshot snapshot)
    {
        output.WriteLine("Nexus research workspace");
        output.WriteLine($"State: {snapshot.State}");
        output.WriteLine($"Project: {project.Title}");
        output.WriteLine($"Workspace: {project.WorkspaceId}");
        output.WriteLine($"Project location: {snapshot.ProjectLocation}");
        output.WriteLine();
        output.WriteLine("Inputs:");
        output.WriteLine($"  search exports: {snapshot.SearchExports}");
        output.WriteLine($"  parser warnings: {snapshot.ParserWarnings}");
        output.WriteLine($"  skipped records: {snapshot.SkippedRecords}");
        output.WriteLine();
        output.WriteLine("Outputs:");
        output.WriteLine($"  import traces: {snapshot.ImportTraces}");
        output.WriteLine($"  dedup analysis: {Presence(snapshot.DeduplicationResultPresent)}");
        output.WriteLine($"  workspace plan: {Presence(snapshot.WorkspacePlanPresent)}");
        output.WriteLine($"  review report: {Presence(snapshot.ReviewReportPresent)}");
        output.WriteLine();
        output.WriteLine("Review:");
        output.WriteLine($"  exact duplicate clusters: {snapshot.ExactDuplicateClusters}");
        output.WriteLine($"  review-required candidates: {snapshot.ReviewRequiredCandidates}");
        output.WriteLine($"  blocking merge gates: {snapshot.BlockingMergeGates}");

        if (snapshot.HasAttention)
        {
            output.WriteLine();
            output.WriteLine("Attention:");
            output.WriteLine($"  Digest mismatches: {snapshot.DigestMismatches}");
            output.WriteLine($"  Missing files: {snapshot.MissingFiles}");
            output.WriteLine($"  Missing import traces: {snapshot.MissingImportTraces}");
            output.WriteLine($"  Invalid paths: {snapshot.InvalidPaths}");
            output.WriteLine($"  Missing generated outputs: {snapshot.MissingGeneratedOutputs}");
        }

        output.WriteLine();
        output.WriteLine($"Next: {snapshot.Next}");
    }

    private static string Presence(bool isPresent) => isPresent ? "present" : "missing";

    private sealed record WorkspaceStatusSnapshot(
        string State,
        string ProjectLocation,
        int SearchExports,
        int ParserWarnings,
        int SkippedRecords,
        int ImportTraces,
        bool DeduplicationResultPresent,
        bool WorkspacePlanPresent,
        bool ReviewReportPresent,
        int ExactDuplicateClusters,
        int ReviewRequiredCandidates,
        int BlockingMergeGates,
        int DigestMismatches,
        int MissingFiles,
        int MissingImportTraces,
        int InvalidPaths,
        int MissingGeneratedOutputs,
        string Next,
        int ExitCode)
    {
        public bool HasAttention =>
            DigestMismatches > 0 ||
            MissingFiles > 0 ||
            MissingImportTraces > 0 ||
            InvalidPaths > 0 ||
            MissingGeneratedOutputs > 0;

        public static WorkspaceStatusSnapshot Create(
            ResearchWorkspaceLocation location,
            ResearchWorkspaceProject project,
            ResearchWorkspaceVerificationReport report,
            string workingDirectory)
        {
            var searchExports = CountSearchExports(project);
            _ = ResearchWorkspaceGenerationVerifier.VerifyCurrent(location, project);
            var planPath = ResearchWorkspacePaths.InProject(location.RootDirectory, project.Outputs.GetValueOrDefault("workspacePlan") ?? ResearchWorkspacePaths.CurrentWorkspacePlan);
            var deduplicationResultPresent = File.Exists(ResearchWorkspacePaths.InProject(location.RootDirectory, project.Outputs.GetValueOrDefault("deduplicationResult") ?? ResearchWorkspacePaths.CurrentDeduplicationResult));
            var workspacePlanPresent = File.Exists(planPath);
            var reviewReportPresent = File.Exists(ResearchWorkspacePaths.InProject(location.RootDirectory, project.Outputs.GetValueOrDefault("reviewReport") ?? ResearchWorkspaceAnalyzer.ReviewReportPath));
            var plan = workspacePlanPresent ? ReadWorkspacePlan(planPath) : null;
            var missingGeneratedOutputs = CountMissingGeneratedOutputs(location, project);
            var state = StateFor(report, searchExports, workspacePlanPresent, plan, missingGeneratedOutputs);
            var exitCode = ExitCodeFor(report, missingGeneratedOutputs);

            return new WorkspaceStatusSnapshot(
                state,
                ProjectLocationLabel(location, workingDirectory),
                searchExports,
                report.ParserWarningCount,
                report.SkippedRecordCount,
                CountExistingImportTraces(location, project),
                deduplicationResultPresent,
                workspacePlanPresent,
                reviewReportPresent,
                plan?.Blocks.Count(block => string.Equals(block.Kind, KnownBlockKinds.DedupCandidateCluster, StringComparison.Ordinal)) ?? 0,
                plan?.Blocks.Count(block => string.Equals(block.Kind, KnownBlockKinds.DedupRecordComparison, StringComparison.Ordinal)) ?? 0,
                plan?.Blocks.Count(block => string.Equals(block.Kind, KnownBlockKinds.HumanGateMergeDecision, StringComparison.Ordinal)) ?? 0,
                report.DigestMismatches.Count,
                report.MissingFiles.Count,
                report.MissingImportTraces.Count,
                report.InvalidPaths.Count,
                missingGeneratedOutputs,
                NextFor(state, report),
                exitCode);
        }

        private static int CountSearchExports(ResearchWorkspaceProject project)
        {
            return project.Inputs.Count(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal));
        }

        private static int CountExistingImportTraces(
            ResearchWorkspaceLocation location,
            ResearchWorkspaceProject project)
        {
            return project.Inputs
                .Where(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal))
                .Count(input =>
                    !string.IsNullOrWhiteSpace(input.ImportTracePath) &&
                    ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, input.ImportTracePath, out var tracePath) &&
                    File.Exists(tracePath));
        }

        private static int CountMissingGeneratedOutputs(
            ResearchWorkspaceLocation location,
            ResearchWorkspaceProject project)
        {
            var missing = 0;
            foreach (var relativePath in project.Outputs.Values)
            {
                if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var outputPath) ||
                    !File.Exists(outputPath))
                {
                    missing++;
                }
            }

            return missing;
        }

        private static WorkspacePlan ReadWorkspacePlan(string planPath)
        {
            var plan = JsonSerializer.Deserialize<WorkspacePlan>(
                File.ReadAllText(planPath),
                UiContractJson.SerializerOptions);
            return plan ?? throw new JsonException("Workspace plan file did not contain an object.");
        }

        private static string StateFor(
            ResearchWorkspaceVerificationReport report,
            int searchExports,
            bool workspacePlanPresent,
            WorkspacePlan? plan,
            int missingGeneratedOutputs)
        {
            if (report.DigestMismatches.Count > 0 ||
                report.MissingFiles.Count > 0 ||
                report.MissingImportTraces.Count > 0 ||
                report.InvalidPaths.Count > 0 ||
                missingGeneratedOutputs > 0)
            {
                return "needs-attention";
            }

            if (searchExports == 0)
            {
                return "initialized";
            }

            if (workspacePlanPresent && plan is not null)
            {
                return HasReviewWork(plan) ? "review-ready" : "analyzed";
            }

            return report.ParserWarningCount > 0 || report.SkippedRecordCount > 0
                ? "imported-with-warnings"
                : "imported";
        }

        private static bool HasReviewWork(WorkspacePlan plan)
        {
            return plan.Mode == BlockMode.Review ||
                plan.Blocks.Any(block =>
                    block.Severity is BlockSeverity.ReviewRequired or BlockSeverity.Blocking or BlockSeverity.Critical ||
                    string.Equals(block.Kind, KnownBlockKinds.HumanGateMergeDecision, StringComparison.Ordinal));
        }

        private static string ProjectLocationLabel(ResearchWorkspaceLocation location, string workingDirectory)
        {
            var root = Path.GetFullPath(location.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(root, current, StringComparison.OrdinalIgnoreCase)
                ? "current folder"
                : "parent workspace";
        }

        private static string NextFor(string state, ResearchWorkspaceVerificationReport report)
        {
            return state switch
            {
                "initialized" => "nexus import search <file> --source <source> --format <format>",
                "imported" or "imported-with-warnings" => "nexus verify",
                "analyzed" => "nexus clusters",
                "review-ready" => "nexus review",
                "needs-attention" when report.DigestMismatches.Count > 0 => "restore the changed file or re-import intentionally.",
                "needs-attention" => "restore missing files or re-run the previous workflow step intentionally.",
                _ => "nexus status"
            };
        }

        private static int ExitCodeFor(ResearchWorkspaceVerificationReport report, int missingGeneratedOutputs)
        {
            if (report.DigestMismatches.Count > 0)
            {
                return ResearchWorkspaceExitCodes.DigestMismatch;
            }

            if (report.MissingFiles.Count > 0 || report.MissingImportTraces.Count > 0 || missingGeneratedOutputs > 0)
            {
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            return report.InvalidPaths.Count > 0
                ? ResearchWorkspaceExitCodes.UsageOrValidationFailure
                : ResearchWorkspaceExitCodes.Success;
        }
    }
}
