using System.Diagnostics;
using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     docs/plan/13 § "Budgets", asserted rather than aspired to.
/// </summary>
/// <remarks>
///     docs/plan/12 § "Performance tests": "Budgets from 13 are asserted in CI with a 20 % tolerance
///     band; exceeding it fails the build, because performance regressions in a tool that runs in a
///     pre-commit hook are user-visible within a day and untraceable a month later."
///     <para>
///         ⚠ <b>Opt-in, by <c>SKALA_PERF=1</c>.</b> Not because the numbers do not matter — they are the
///         milestone — but because a wall-clock assertion on a shared CI runner with noisy neighbours fails
///         for reasons that have nothing to do with the commit, and a test that cries wolf is a test people
///         delete. It runs in a dedicated CI job on a runner doing nothing else. Locally,
///         <c>SKALA_PERF=1 dotnet test</c>.
///     </para>
///     <para>
///         ⚠ <b>Measured as a loop divided by N, never as one run.</b> This was learned the hard way in M7:
///         a Python <c>subprocess</c> harness reports <b>38 ms</b> for an <i>empty</i> NativeAOT binary and
///         <b>2 ms</b> for <c>/usr/bin/true</c> on the same machine — an artefact large enough to hide the
///         entire 40 ms budget. The harness has to be cheaper than the thing it measures, and it has to be
///         shown to be, which is why <see cref="ProcessStartFloor" /> is measured and reported alongside
///         every result rather than assumed.
///     </para>
/// </remarks>
[Trait("Category", "Performance")]
public sealed class PerformanceBudgetTests {
    /// <summary>docs/plan/12: "a 20 % tolerance band".</summary>
    const double Tolerance = 1.20;

    static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("SKALA_PERF"), "1", StringComparison.Ordinal);

    /// <summary>
    ///     ⚠ The row that matters. doc 13: "`format` one 500-line file | Warm (daemon) | &lt; 40 ms |
    ///     the agent hook; includes process start". Before M7 this measured 60–70 ms and essentially all
    ///     of it was the client's own process start.
    /// </summary>
    [Fact]
    public void WarmSingleFileFormat_IsUnderFortyMilliseconds() {
        Assert.SkipUnless(Enabled, "Set SKALA_PERF=1 to run the wall-clock budget assertions.");

        var client = NativeLayout.Client;
        Assert.SkipWhen(client is null, "No native layout. Run `./build.sh Native` first.");

        using var bed = new DaemonBed();
        bed.WaitUntilListening();

        var floor = ProcessStartFloor();
        var measured = Median(60, () => Run(client!, "format", "--check", bed.Subject));

        // ⚠ Prove the daemon was actually consulted before believing the number. The client execs
        // the full tool on any failure — by design, so that it can never be the reason a command
        // fails — which means a broken warm path measures as a working slow one. The first version
        // of this test measured 218 ms against a 40 ms budget for exactly that reason: the bed was
        // not a git repository, so there was no repository root, so there was no socket to look
        // for. A performance test that cannot tell "slow" from "not running" is not a test.
        var status = bed.Status();
        Assert.True(
            ParseMegabytes(status, "misses, ") >= 0 && !status.Contains(" 0 hits,", StringComparison.Ordinal),
            $"the daemon served nothing, so this measured the fallback path and not the warm one: {status}"
        );

        Report("format one file, warm (daemon)", measured, 40, floor);
        var attributable = measured - floor;
        Assert.True(
            attributable <= 40 * Tolerance,
            $"docs/plan/13's warm single-file budget is 40 ms with a 20 % band. Measured {measured:F2} ms "
            + $"minus a {floor:F2} ms harness floor = {attributable:F2} ms attributable. This is the agent "
            + "hook; it is the one budget the milestone exists to meet."
        );
    }

    /// <summary>doc 13: "`format` one 500-line file | Cold | 250 ms".</summary>
    [Fact]
    public void ColdSingleFileFormat_IsUnderTwoHundredAndFiftyMilliseconds() {
        Assert.SkipUnless(Enabled, "Set SKALA_PERF=1 to run the wall-clock budget assertions.");

        var tool = NativeLayout.Tool;
        Assert.SkipWhen(tool is null, "No native layout. Run `./build.sh Native` first.");

        using var bed = new DaemonBed(startDaemon: false);
        var measured = Median(15, () => Run(tool!, "format", "--check", bed.Subject, noDaemon: true));

        Report("format one file, cold (no daemon)", measured, 250, ProcessStartFloor());
        Assert.True(
            measured <= 250 * Tolerance,
            $"docs/plan/13's cold single-file budget is 250 ms with a 20 % band. Measured {measured:F2} ms."
        );
    }

    /// <summary>
    ///     doc 13: "Daemon RSS, idle after a corpus run | &lt; 1.5 GB | compilations dropped under
    ///     pressure". Asserted after a few hundred formats rather than after a whole corpus, which is
    ///     what the nightly job is for — but the bound is the same bound.
    /// </summary>
    [Fact]
    public void DaemonResidentSet_StaysUnderTheBudget() {
        Assert.SkipUnless(Enabled, "Set SKALA_PERF=1 to run the wall-clock budget assertions.");

        var tool = NativeLayout.Tool;
        Assert.SkipWhen(tool is null, "No native layout. Run `./build.sh Native` first.");

        using var bed = new DaemonBed();
        bed.WaitUntilListening();

        for (var i = 0; i < 200; i++) {
            Run(tool!, "format", "--check", bed.Subject);
        }

        var status = bed.Status();
        var rss = ParseMegabytes(status, "RSS ");

        Report("daemon RSS after 200 formats", rss, 1500, 0);
        Assert.True(
            rss > 0 && rss <= 1500,
            $"docs/plan/13 budgets daemon RSS at under 1.5 GB. `daemon status` says: {status}"
        );
    }

    /// <summary>
    ///     ⚠ Reported with every result, subtracted from it, and never assumed.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is not a rounding correction; on this harness it is half the budget.</b>
    ///     <c>Process.Start</c> from a .NET test host costs <b>~22 ms</b> to spawn <c>/usr/bin/true</c>
    ///     on the reference machine, against <b>1.9 ms</b> for the same binary from a shell loop — the
    ///     runtime forks a large process and copies its environment. Against a 40 ms budget that is not
    ///     noise, so the floor is measured every run with the same spawner and subtracted, and what is
    ///     asserted is the time attributable to Skala including <i>its own</i> process start, which is
    ///     what doc 13's row means.
    ///     <para>
    ///         For the record, the same operation measured directly — 150 runs of the published client in a
    ///         shell loop, wall time divided by N — is <b>8.65 ms</b>. The corrected figure here is larger
    ///         because the harness's overhead is not perfectly constant across binaries of different sizes.
    ///         The number to quote is the shell-loop one; the number to <i>regress against</i> is this one,
    ///         because it is the one CI can compute unattended.
    ///     </para>
    /// </remarks>
    static double ProcessStartFloor() {
        var probe = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.SystemDirectory, "cmd.exe")
            : "/usr/bin/true";

        return File.Exists(probe)
            ? Median(40, () => Start(probe, OperatingSystem.IsWindows() ? ["/c", "exit"] : [], null))
            : 0;
    }

    static double Median(int runs, Action action) {
        // Three warm-ups, uncounted: the first run pays the page cache and, for the daemon path,
        // the daemon's own first-format cost.
        for (var i = 0; i < 3; i++) {
            action();
        }

        var samples = new double[runs];
        for (var i = 0; i < runs; i++) {
            var stopwatch = Stopwatch.StartNew();
            action();
            samples[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        return samples[runs / 2];
    }

    static double Run(string executable, string command, string flag, string file, bool noDaemon = false) =>
        Start(executable, [command, flag, file], noDaemon ? "1" : null);

    static double Start(string executable, string[] arguments, string? noDaemon) {
        var info = new ProcessStartInfo(executable) {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        if (noDaemon is not null) {
            info.Environment["SKALA_NO_DAEMON"] = noDaemon;
        }

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(info)!;
        process.WaitForExit();
        var elapsed = stopwatch.Elapsed.TotalMilliseconds;

        // ⚠ Drained *after* the clock stops, and this is not a detail. Reading the two pipes to EOF
        // sequentially before WaitForExit — the obvious way to write this — waits for stderr's EOF
        // after stdout's and charges that latency to the process being measured: it put ~20 ms on
        // every sample, half the budget under test. Safe to defer only because these commands emit
        // one line at most, so nothing can fill a pipe buffer and deadlock.
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        return elapsed;
    }

    static double ParseMegabytes(string status, string marker) {
        var at = status.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) {
            return -1;
        }

        var rest = status[(at + marker.Length)..];
        var end = rest.IndexOf(' ', StringComparison.Ordinal);
        return double.TryParse(
            end < 0 ? rest : rest[..end],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value
        )
                ? value
                : -1;
    }

    static void Report(string row, double measured, double budget, double floor) =>
        TestContext.Current.TestOutputHelper?.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{row}: measured {measured:F2} ms, budget {budget:F0} ms (+20 % = {budget * Tolerance:F0}), process-start floor {floor:F2} ms"
            )
        );
}

/// <summary>The published `skala` / `skala-tool` pair, if `./build.sh Native` has been run.</summary>
public static class NativeLayout {
    static string Directory =>
        Path.Combine(
            CliRunner.RepositoryRoot,
            "artifacts",
            "native",
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier
        );

    static string? Find(string name) {
        var path = Path.Combine(Directory, name + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        return File.Exists(path) ? path : null;
    }

    public static string? Client => Find("skala");

    public static string? Tool => Find("skala-tool");
}
