using System.Diagnostics;
using System.Text;
using Rikarin.Skala.Protocol;

namespace Rikarin.Skala.Client;

/// <summary>
///     <c>skala</c>: a socket, a JSON writer, and a way to hand everything else to the real tool.
/// </summary>
/// <remarks>
///     docs/plan/13 § "Startup". The measurement that produced this type: <c>skala daemon status</c>,
///     doing no work whatever, cost <b>95 ms median</b> on the reference machine, because the one
///     <c>skala</c> executable referenced Rikarin.Skala.Analysis and so loaded Roslyn before
///     <c>Main</c>. The budget for a warm single-file format — the agent hook, the tightest deadline in
///     the product — is 40 ms for the entire operation. The daemon was already answering in single
///     digits; the client was the whole cost.
///     <para>
///         ⚠ <b>Two rules govern every line here.</b>
///     </para>
///     <list type="number">
///         <item>
///             <b>Reference nothing.</b> Only Rikarin.Skala.Protocol, which itself has no references. Roslyn is
///             not AOT-friendly and is not needed to write a path down a socket. Anything added here is paid for
///             on every hook invocation, for ever.
///         </item>
///         <item>
///             <b>Never be the reason a command fails.</b> Everything that is not the one hot path — and every
///             failure on it — becomes an exec of the full tool. The client has no opinions, no error messages
///             of its own beyond one, and no behaviour the full tool does not have. If this file ever decides
///             something the full tool would decide differently, that is a formatting difference between two
///             developers on one repository, which is the failure the whole product exists to prevent.
///         </item>
///     </list>
/// </remarks>
public static class ThinClient {
    /// <summary>
    ///     ⚠ Short. A daemon that has not answered in a quarter second is not going to make the 40 ms
    ///     budget, and every millisecond spent waiting is a millisecond added to the fallback that is
    ///     about to happen anyway.
    /// </summary>
    static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(250);

    public static int Run(string[] args) {
        try {
            if (TryServe(args, out var exitCode)) {
                return exitCode;
            }
        } catch (IOException) {
            // Fall through to the tool. See rule 2.
        } catch (UnauthorizedAccessException) { } catch (InvalidDataException) { }

        return Fallback.Exec(args);
    }

    /// <summary>
    ///     The one path the client serves itself: <c>skala format &lt;one existing file&gt;</c>, with a
    ///     daemon already listening.
    /// </summary>
    /// <remarks>
    ///     ⚠ The disqualifiers mirror <c>DaemonUse.TryFormat</c> in the full tool exactly, and they are
    ///     deliberately conservative: <c>--staged</c> touches the git index, <c>--range</c> and
    ///     <c>--option</c> change what "format this file" means, and anything the client does not
    ///     recognise might. A flag this client has never heard of is a reason to hand the whole
    ///     invocation over, not a reason to guess — which is why the check is a whitelist of the
    ///     harmless flags rather than a blacklist of the dangerous ones. A new option added to the full
    ///     tool is therefore safe by default: the client stops serving and execs, and the only cost is
    ///     speed.
    /// </remarks>
    static bool TryServe(string[] args, out int exitCode) {
        exitCode = 0;

        if (Environment.GetEnvironmentVariable("SKALA_NO_DAEMON") == "1") {
            return false;
        }

        if (args.Length < 2 || !string.Equals(args[0], "format", StringComparison.Ordinal)) {
            return false;
        }

        string? file = null;
        var check = false;
        var quiet = false;
        for (var i = 1; i < args.Length; i++) {
            var argument = args[i];
            if (argument.Length == 0) {
                return false;
            }

            if (argument[0] == '-') {
                // The two flags whose meaning the client can reproduce exactly. Everything else,
                // including `--` and any option taking a value, goes to the tool.
                if (string.Equals(argument, "--check", StringComparison.Ordinal)) {
                    check = true;
                    continue;
                }

                if (string.Equals(argument, "--quiet", StringComparison.Ordinal)) {
                    quiet = true;
                    continue;
                }

                return false;
            }

            if (file is not null) {
                return false; // More than one path: the tool's parallel loop is the right answer.
            }

            file = argument;
        }

        if (file is null || !File.Exists(file)) {
            return false;
        }

        var full = Path.GetFullPath(file);
        var root = RepositoryRoot.Find(full);
        if (root is null || !DaemonTransport.MightExist(root)) {
            // No daemon to ask. The tool does the work, and leaves one behind for next time — the
            // lazy start doc 13 relies on to make the warm row reachable without a person running
            // `skala daemon run` by hand.
            return false;
        }

        var response = Send(root, new DaemonRequest { Command = "format", Path = full });
        if (response is not { Ok: true, Formatted: not null }) {
            return false;
        }

        // ⚠ Diagnostics mean the file did not parse, or the safety net fired. The tool renders those
        // and the client will not learn how.
        if (response.Diagnostics.Count > 0) {
            return false;
        }

        if (!check && response.Changed) {
            // ⚠ UTF-8, no BOM, and the daemon's exact bytes. Writing through a StreamWriter with a
            // platform default here would be a formatter that changes line endings on Windows.
            File.WriteAllBytes(full, new UTF8Encoding(false).GetBytes(response.Formatted));
        }

        if (response.Changed) {
            Console.Out.Write(RepositoryRoot.Relative(root, full));
            Console.Out.Write('\n');
        }

        if (!quiet) {
            Console.Out.Write(Summary(response.Changed, check));
        }

        // ⚠ 2 — `ExitCodes.FormattingNeeded`, docs/plan/09 § "Exit codes". It is a literal here and
        // nowhere else, because this assembly references neither Core nor Roslyn on purpose
        // (docs/plan/13 § "Startup") and `ExitCodes` lives in Core. `ClientAgreesWithToolTests` runs
        // both binaries and compares the codes, which is what holds the two halves together;
        // getting this wrong makes the client disagree with the tool about whether a hook passed,
        // which is the one thing this class must never do.
        //
        // ⚠ It read 1 until M9, matching a `FormatCommand.ChangesFound` that was itself the
        // documented table read backwards. Both were wrong together, so the agreement test passed.
        exitCode = check && response.Changed ? 2 : 0;
        return true;
    }

    /// <summary>
    ///     `FormatCommand`'s closing line, reproduced exactly for the one-file case.
    /// </summary>
    /// <remarks>
    ///     ⚠ A second implementation of the tool's rendering, which is a thing to be uncomfortable
    ///     about — but the alternative is a client whose output differs from the tool's, and a hook or a
    ///     human reading two different answers to one question is worse than a duplicated format string.
    ///     It is only ever the one-file case, so `changed` is 0 or 1 and `left alone` is its complement.
    ///     `ClientAgreesWithToolTests` compares the two byte for byte; this is why that test exists.
    /// </remarks>
    static string Summary(bool changed, bool check) =>
        (changed ? "1 file " : "0 files ")
        + (check ? "would be reformatted" : "reformatted")
        + ", "
        + (changed ? "0" : "1")
        + " left alone\n";

    static DaemonResponse? Send(string root, DaemonRequest request) {
        using var stream = DaemonTransport.Connect(root, Budget);
        if (stream is null) {
            return null;
        }

        using var cancellation = new CancellationTokenSource(Budget);
        DaemonProtocol.WriteAsync(stream, request, cancellation.Token).GetAwaiter().GetResult();
        return DaemonProtocol.ReadResponseAsync(stream, cancellation.Token).GetAwaiter().GetResult();
    }
}

/// <summary>Where the full tool is, and how to become it.</summary>
public static class Fallback {
    /// <summary>
    ///     Runs the full tool with the same arguments and returns its exit code.
    /// </summary>
    /// <remarks>
    ///     ⚠ This costs one extra process start (~5 ms) on every command that is not a warm single-file
    ///     format. That is the price doc 13 § "Startup" mitigation 2 says is accepted — "the fallback
    ///     path when no daemon can start must still be the full tool, which means shipping both" — and
    ///     it is charged against commands that already take seconds, not against the 40 ms one.
    ///     <para>
    ///         ⚠ Not <c>execve</c>, though that would save the wait: it does not exist on Windows, and a
    ///         client that behaves differently on one platform is worse than one that is uniformly 5 ms
    ///         slower. Standard streams are inherited rather than redirected, so a tool that renders colour,
    ///         reads stdin (`--stdin`, the LSP, the MCP server) or writes a progress line behaves exactly as
    ///         it does when invoked directly.
    ///     </para>
    /// </remarks>
    public static int Exec(string[] args) {
        var tool = Locate();
        if (tool is null) {
            Console.Error.WriteLine(
                "skala: cannot find `skala-tool` beside this executable. The thin client serves only "
                + "warm single-file formatting; everything else needs the full tool. Set SKALA_TOOL "
                + "to its path, or reinstall — the two ship together."
            );

            return 5; // ExitCodes.InternalError
        }

        var info = new ProcessStartInfo(tool) { UseShellExecute = false };
        foreach (var argument in args) {
            info.ArgumentList.Add(argument);
        }

        try {
            using var process = Process.Start(info);
            if (process is null) {
                return 5;
            }

            process.WaitForExit();
            return process.ExitCode;
        } catch (System.ComponentModel.Win32Exception exception) {
            Console.Error.WriteLine("skala: cannot start " + tool + ": " + exception.Message);
            return 5;
        }
    }

    /// <summary>
    ///     ⚠ Beside this executable first, and only then <c>SKALA_TOOL</c> or the path. Two Skala
    ///     versions formatting one repository is the failure the version pinning in doc 11 exists to
    ///     prevent, and picking up whatever `skala-tool` happens to be on the PATH is exactly how that
    ///     happens.
    /// </summary>
    public static string? Locate() {
        var directory = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
        var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        if (directory is { Length: > 0 }) {
            var beside = Path.Combine(directory, "skala-tool" + suffix);
            if (File.Exists(beside)) {
                return beside;
            }
        }

        var configured = Environment.GetEnvironmentVariable("SKALA_TOOL");
        return configured is { Length: > 0 } && File.Exists(configured) ? configured : null;
    }
}

/// <summary>
///     The repository root, and a path relative to it.
/// </summary>
/// <remarks>
///     ⚠ A third implementation of "where is the repository", and it has to agree with the two in the
///     full tool exactly — the client and the tool must produce identical output for the same command.
///     It is duplicated rather than shared because sharing it would mean referencing an assembly that
///     carries Roslyn, which is the whole thing this project exists to avoid. The agreement is held by
///     a test rather than by the type system: see <c>ThinClientTests</c>.
/// </remarks>
public static class RepositoryRoot {
    public static string? Find(string path) {
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        while (directory is not null) {
            // ⚠ A file *or* a directory: in a git worktree and in a submodule, `.git` is a file
            // containing `gitdir: …`. Testing only for a directory is a bug the full tool's CLI had.
            var marker = Path.Combine(directory, ".git");
            if (Directory.Exists(marker) || File.Exists(marker)) {
                return directory;
            }

            var parent = Path.GetDirectoryName(directory);
            directory = string.Equals(parent, directory, StringComparison.Ordinal) ? null : parent;
        }

        return null;
    }

    /// <summary>Repository-relative and forward-slashed, matching <c>SarifWriter.Relative</c>.</summary>
    public static string Relative(string root, string path) {
        var normalised = path.Replace('\\', '/');
        var trimmed = root.Replace('\\', '/').TrimEnd('/');
        if (trimmed.Length == 0 || !Path.IsPathRooted(path)) {
            return normalised;
        }

        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!normalised.StartsWith(trimmed, comparison)
            || (normalised.Length != trimmed.Length && normalised[trimmed.Length] != '/')) {
            return normalised;
        }

        return Path.GetRelativePath(trimmed, normalised).Replace('\\', '/');
    }
}
