using System.Diagnostics;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
/// The thin client and the full tool must produce the same answer, byte for byte and code for code.
/// </summary>
/// <remarks>
/// ⚠ This is the invariant the whole M7 split rests on, and it is the one that cannot be held by the
/// type system: the client is a separate assembly that references neither the tool nor Roslyn, so
/// every decision it makes is a second implementation of a decision the tool already makes.
/// docs/plan/11's correctness rule — "every command works identically with
/// <c>SKALA_NO_DAEMON=1</c>" — now has a second half: every command works identically through the
/// client and through the tool.
/// <para>
/// ⚠ It has already caught one: the client returned <b>2</b> from <c>format --check</c> on a file
/// that needed formatting, because <c>ExitCodes.FormattingNeeded</c> is 2. But <c>format</c> did
/// not use <c>ExitCodes</c> — it used <c>FormatCommand.ChangesFound</c>, which was <b>1</b>, and
/// <c>FormatCommand.Failed</c>, which was 2. A pre-commit hook reading the exit code would have
/// passed where the tool failed and vice versa.
/// </para>
/// <para>
/// ⚠ And the resolution was the opposite of the one taken at the time: the <b>client</b> was right
/// and the tool was wrong. <c>ExitCodes</c> matched docs/plan/09 § "Exit codes" and the README all
/// along; <c>FormatCommand</c>'s private pair was the documented table inverted. Making the client
/// agree with the tool made both wrong, and this test went green on the wrong number — which is
/// what an agreement test does when it is the only thing asserted. M9 moved <c>ExitCodes</c> into
/// Core so the two cannot diverge again, and <see cref="ExitCodeContractTests"/> asserts the codes
/// against the document rather than against each other.
/// </para>
/// </remarks>
public sealed class ClientAgreesWithToolTests {
    [Fact]
    public void FormatCheck_OnAFileThatNeedsIt_AgreesOnOutputAndExitCode() {
        Assert.SkipWhen(NativeLayout.Client is null, "No native layout. Run `./build.sh Native` first.");

        using var bed = new DaemonBed();
        bed.WaitUntilListening();

        // ⚠ Make the file need formatting, or both halves agree on "nothing to do" and the test
        // asserts nothing.
        File.WriteAllText(bed.Subject, "class  A{ void  B( ){} }\n");

        var viaTool = Run(NativeLayout.Tool!, "format", "--check", bed.Subject);
        var viaClient = Run(NativeLayout.Client!, "format", "--check", bed.Subject);

        Assert.Equal(viaTool.Code, viaClient.Code);
        Assert.Equal(viaTool.Output, viaClient.Output);

        // And the contract the README states and the pre-commit hook reads.
        Assert.Equal(2, viaTool.Code);
    }

    [Fact]
    public void FormatCheck_OnAFileThatDoesNot_AgreesOnZero() {
        Assert.SkipWhen(NativeLayout.Client is null, "No native layout. Run `./build.sh Native` first.");

        using var bed = new DaemonBed();
        bed.WaitUntilListening();

        // Format it first, so that both halves see a file with nothing to do.
        Run(NativeLayout.Tool!, "format", bed.Subject);

        var viaTool = Run(NativeLayout.Tool!, "format", "--check", bed.Subject);
        var viaClient = Run(NativeLayout.Client!, "format", "--check", bed.Subject);

        Assert.Equal(0, viaTool.Code);
        Assert.Equal(viaTool.Code, viaClient.Code);
        Assert.Equal(viaTool.Output, viaClient.Output);
    }

    /// <summary>
    /// ⚠ Everything the client does not serve must reach the tool unchanged. The client execs rather
    /// than reimplements, so this is really a test that <c>Fallback.Locate</c> found the tool beside
    /// it — the failure mode being a client that cannot find its other half and says so on every
    /// command.
    /// </summary>
    [Fact]
    public void ACommandTheClientDoesNotServe_IsHandedToTheToolUnchanged() {
        Assert.SkipWhen(NativeLayout.Client is null, "No native layout. Run `./build.sh Native` first.");

        var viaTool = Run(NativeLayout.Tool!, "explain", "SK1005");
        var viaClient = Run(NativeLayout.Client!, "explain", "SK1005");

        Assert.Equal(viaTool.Code, viaClient.Code);
        Assert.Equal(viaTool.Output, viaClient.Output);
        Assert.Contains("SK1005", viaClient.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ With the daemon deliberately unreachable, the client must still be correct — just slower.
    /// A client that only works when a daemon happens to be up is a client that fails on the first
    /// invocation in every repository.
    /// </summary>
    [Fact]
    public void WithNoDaemon_TheClientStillAgrees() {
        Assert.SkipWhen(NativeLayout.Client is null, "No native layout. Run `./build.sh Native` first.");

        using var bed = new DaemonBed(startDaemon: false);
        File.WriteAllText(bed.Subject, "class  A{ void  B( ){} }\n");

        var viaTool = Run(NativeLayout.Tool!, "format", "--check", bed.Subject, noDaemon: true);
        var viaClient = Run(NativeLayout.Client!, "format", "--check", bed.Subject, noDaemon: true);

        Assert.Equal(viaTool.Code, viaClient.Code);
        Assert.Equal(viaTool.Output, viaClient.Output);
    }

    static (int Code, string Output) Run(string executable, params string[] arguments) {
        var info = new ProcessStartInfo(executable) {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        foreach (var argument in arguments) {
            info.ArgumentList.Add(argument);
        }

        return Start(info);
    }

    static (int Code, string Output) Run(string executable, string a, string b, string c, bool noDaemon) {
        var info = new ProcessStartInfo(executable) {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        info.ArgumentList.Add(a);
        info.ArgumentList.Add(b);
        info.ArgumentList.Add(c);
        if (noDaemon) {
            info.Environment["SKALA_NO_DAEMON"] = "1";
        }

        return Start(info);
    }

    static (int Code, string Output) Start(ProcessStartInfo info) {
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
