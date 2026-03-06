using System.Diagnostics;

internal static class RoslynticApp
{
    internal static Func<string, CancellationToken, Task<AnalysisExecutionResult>> AnalyzeAsyncCore { get; set; } = AnalyzerPipeline.AnalyzeAsync;
    internal static TimeSpan AnalysisTimeout { get; set; } = TimeSpan.FromSeconds(CliContract.AnalysisTimeoutSeconds);

    public static async Task<int> RunAsync(string[] args)
    {
        var runStopwatch = Stopwatch.StartNew();
        var workspaceLoadDurationMs = 0L;
        var rulesDurationMs = 0L;
        var outputDurationMs = 0L;
        var projectsCount = 0;
        var documentsCount = 0;
        var rulesExecutedCount = CliContract.RulesExecutedCount;
        var findingsError = 0;
        var findingsWarning = 0;
        var findingsNote = 0;

        if (args.Length > 0 && string.Equals(args[0], "--", StringComparison.Ordinal))
        {
            args = args[1..];
        }

        if (args.Length < 2 || !string.Equals(args[0], CliContract.CheckCommand, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: roslyntic check <path> [--format sarif|json]");
            return CliContract.ExecutionErrorExitCode;
        }

        var targetPath = args[1];

        try
        {
            var format = ParseFormat(args);
            using var timeoutCts = new CancellationTokenSource(AnalysisTimeout);
            var analysisResult = await AnalyzeAsyncCore(targetPath, timeoutCts.Token);
            workspaceLoadDurationMs = analysisResult.Metrics.WorkspaceLoadDurationMs;
            rulesDurationMs = analysisResult.Metrics.RulesDurationMs;
            projectsCount = analysisResult.Metrics.Projects;
            documentsCount = analysisResult.Metrics.Documents;
            rulesExecutedCount = analysisResult.Metrics.RulesExecuted;
            var sortedDiagnostics = DiagnosticSorter.Sort(analysisResult.Findings);
            findingsError = sortedDiagnostics.Count(item => string.Equals(item.Level, "error", StringComparison.OrdinalIgnoreCase));
            findingsWarning = sortedDiagnostics.Count(item => string.Equals(item.Level, "warning", StringComparison.OrdinalIgnoreCase));
            findingsNote = sortedDiagnostics.Count(item => string.Equals(item.Level, "note", StringComparison.OrdinalIgnoreCase));
            var outputStopwatch = Stopwatch.StartNew();
            var payload = format switch
            {
                CliContract.JsonFormat => JsonOutputWriter.Write(sortedDiagnostics),
                _ => SarifOutputWriter.Write(sortedDiagnostics)
            };
            outputStopwatch.Stop();
            outputDurationMs = outputStopwatch.ElapsedMilliseconds;

            Console.Out.Write(payload);
            var exitCode = sortedDiagnostics.Count == 0 ? CliContract.NoFindingsExitCode : CliContract.FindingsExitCode;
            Console.Error.WriteLine($"event=run_end run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} exitCode={exitCode}");
            return exitCode;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"error.category=rule_execution_failure error.type=timeout run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} message=Analysis timed out after {AnalysisTimeout.TotalSeconds:0.###} seconds.");
            Console.Error.WriteLine($"event=run_end run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} exitCode={CliContract.ExecutionErrorExitCode}");
            return CliContract.ExecutionErrorExitCode;
        }
        catch (WorkspaceLoadException ex)
        {
            Console.Error.WriteLine($"error.category=workspace_load_failure run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} message={ex.Message}");
            Console.Error.WriteLine("Execution failed. See diagnostic logs for details.");
            Console.Error.WriteLine($"event=run_end run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} exitCode={CliContract.ExecutionErrorExitCode}");
            return CliContract.ExecutionErrorExitCode;
        }
        catch (RuleExecutionException ex)
        {
            Console.Error.WriteLine($"error.category=rule_execution_failure run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} message={ex.Message}");
            Console.Error.WriteLine("Execution failed. See diagnostic logs for details.");
            Console.Error.WriteLine($"event=run_end run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} exitCode={CliContract.ExecutionErrorExitCode}");
            return CliContract.ExecutionErrorExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error.category=unexpected_exception run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} message={ex.Message}");
            Console.Error.WriteLine("Execution failed. See diagnostic logs for details.");
            Console.Error.WriteLine($"event=run_end run.durMs={runStopwatch.ElapsedMilliseconds} workspace_load.durMs={workspaceLoadDurationMs} rules.durMs={rulesDurationMs} output_write.durMs={outputDurationMs} projects={projectsCount} documents={documentsCount} rulesExecuted={rulesExecutedCount} findings.error={findingsError} findings.warning={findingsWarning} findings.note={findingsNote} exitCode={CliContract.ExecutionErrorExitCode}");
            return CliContract.ExecutionErrorExitCode;
        }
    }

    private static string ParseFormat(string[] args)
    {
        for (var i = 2; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], CliContract.FormatOption, StringComparison.Ordinal))
            {
                var value = args[i + 1];
                if (string.Equals(value, CliContract.JsonFormat, StringComparison.OrdinalIgnoreCase))
                {
                    return CliContract.JsonFormat;
                }

                if (string.Equals(value, CliContract.SarifFormat, StringComparison.OrdinalIgnoreCase))
                {
                    return CliContract.SarifFormat;
                }

                throw new InvalidOperationException($"Unsupported format: {value}");
            }
        }

        return CliContract.SarifFormat;
    }
}

internal static class CliContract
{
    public const string CheckCommand = "check";
    public const string FormatOption = "--format";
    public const string SarifFormat = "sarif";
    public const string JsonFormat = "json";
    public const int NoFindingsExitCode = 0;
    public const int FindingsExitCode = 1;
    public const int ExecutionErrorExitCode = 2;
    public const int AnalysisTimeoutSeconds = 120;
    public const int RulesExecutedCount = 2;
    public const int ComplexityThreshold = 15;
    public const string ArchRuleId = "AGARCH0001";
    public const string ComplexityRuleId = "AGCOMP0001";
}
