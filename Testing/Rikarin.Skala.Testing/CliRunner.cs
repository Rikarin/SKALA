using System.Diagnostics;
using System.Reflection;
using SystemAssembly = System.Reflection.Assembly;

namespace Rikarin.Skala.Testing;

public sealed record CliRun(int ExitCode, string StandardOutput, string StandardError) {
    public IEnumerable<string> Lines => StandardOutput.Split('\n').Select(static line => line.TrimEnd('\r'));
}

/// <summary>
/// Drives the built <c>skala</c> binary as a process.
/// </summary>
/// <remarks>
/// ⚠ Nothing references <c>Rikarin.Skala.Cli</c> (docs/plan/02 § "The project graph"), the tests
/// included. They exercise the real command surface — argument parsing, exit codes and the text a
/// user actually sees — which is the only part of the tool that is a contract (ADR-010).
/// <para>
/// It lives in <c>Rikarin.Skala.Testing</c> rather than in the CLI's own test project because
/// docs/plan/02 puts the harness there and because two command test projects would otherwise each
/// grow their own copy.
/// </para>
/// </remarks>
public static class CliRunner {
    public static string RepositoryRoot { get; } = Metadata("SkalaRepositoryRoot");

    /// <summary>The built binary, for tests that need to drive it from another working directory.</summary>
    public static string Assembly { get; } = Metadata("SkalaCliAssembly").Replace('/', Path.DirectorySeparatorChar);

    public static string Template { get; } = Path.Combine(RepositoryRoot, "editor_config_template");

    public static CliRun Run(params string[] arguments) => RunWith(null, arguments);

    /// <summary>
    /// The same run with extra environment variables, for the contracts that have no other trigger.
    /// </summary>
    /// <remarks>
    /// ⚠ One caller: the exit-code-5 row. No input trips the formatter's safety net any more — the
    /// three that ever did are fixed and retired — so <c>SKALA_FORCE_SK9099</c> is how that row
    /// keeps a behavioural test. See <c>CSharpFormatter.ForcedVerificationFailure</c>.
    /// </remarks>
    public static CliRun RunWith(IReadOnlyDictionary<string, string>? environment, params string[] arguments) {
        if (!File.Exists(Assembly)) {
            throw new InvalidOperationException(
                $"The skala binary is not at '{Assembly}'. Build the solution (or run ./build.sh Test) before running these tests."
            );
        }

        var start = new ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepositoryRoot,
            UseShellExecute = false
        };

        if (environment is not null) {
            foreach (var (key, value) in environment) {
                start.Environment[key] = value;
            }
        }

        start.ArgumentList.Add(Assembly);
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
        SystemAssembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value
        ?? throw new InvalidOperationException($"{key} was not stamped into the test assembly.");
}
