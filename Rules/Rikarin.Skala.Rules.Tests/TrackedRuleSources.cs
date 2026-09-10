using System.Diagnostics;
using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The analyzer sources as <c>git</c> lists them, for the tests that audit rule source text.
/// </summary>
/// <remarks>
///     ⚠ <b><c>git ls-files</c> rather than the working tree, and an empty listing is a broken
///     instrument rather than a clean tree.</b> A ledger test that scans nothing passes every
///     assertion it makes, so both callers assert the listing is large before believing what it
///     contains — a zero from a check that did not run and a zero from a clean tree are the same
///     zero.
///     <para>
///         ⚠ Extracted rather than copied. <see cref="RewriteGuardReachTests" /> had this first and
///         <see cref="FrameworkAvailabilityReachTests" /> needs exactly it; a second copy is ~40
///         tokens of identical shape and <c>SK7020</c> reports the clone — which is the duplication
///         gate working on the test project the way it works on everything else.
///     </para>
/// </remarks>
static class TrackedRuleSources {
    public static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!;

    public static List<string> All() {
        var process = Process.Start(
            new ProcessStartInfo("git", "ls-files -- Rules/Rikarin.Skala.Rules/*.cs") {
                WorkingDirectory = RepositoryRoot, RedirectStandardOutput = true
            }
        )!;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToList();
    }
}
