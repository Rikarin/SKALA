using System.Diagnostics;
using System.Reflection;

namespace Rikarin.Skala.Cli.Tests;

public sealed record CliRun(int ExitCode, string StandardOutput, string StandardError) {
    public IEnumerable<string> Lines => StandardOutput.Split('\n').Select(static line => line.TrimEnd('\r'));
}

/// <summary>
/// Drives the built <c>skala</c> binary as a process.
/// </summary>
/// <remarks>
/// ⚠ Nothing references <c>Rikarin.Skala.Cli</c> (docs/plan/02 § "The project graph"), these tests
/// included. They exercise the real command surface — argument parsing, exit codes and the text a
/// user actually sees — which is the only part of the tool that is a contract (ADR-010).
/// </remarks>
public static class CliRunner {
    public static string RepositoryRoot { get; } = Metadata("SkalaRepositoryRoot");

    static string CliAssembly { get; } = Metadata("SkalaCliAssembly").Replace('/', Path.DirectorySeparatorChar);

    public static string Template { get; } = Path.Combine(RepositoryRoot, "editor_config_template");

    public static CliRun Run(params string[] arguments) {
        if (!File.Exists(CliAssembly)) {
            throw new InvalidOperationException(
                $"The skala binary is not at '{CliAssembly}'. Build the solution (or run ./build.sh Test) before running these tests.");
        }

        var start = new ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepositoryRoot,
            UseShellExecute = false
        };

        start.ArgumentList.Add(CliAssembly);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("dotnet did not start.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliRun(process.ExitCode, output, error);
    }

    static string Metadata(string key) =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value
        ?? throw new InvalidOperationException($"{key} was not stamped into the test assembly.");
}
