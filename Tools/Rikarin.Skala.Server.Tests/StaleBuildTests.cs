using Rikarin.Skala.Protocol;
using Rikarin.Skala.Server;

namespace Rikarin.Skala.Server.Tests;

/// <summary>
///     A directory that looks like a Skala install, and can be "rebuilt" underneath a running daemon.
/// </summary>
/// <remarks>
///     ⚠ Copies of the real assemblies rather than fabricated files, because
///     <see cref="BuildIdentity" /> reads module MVIDs out of PE images and a plausible-looking
///     stand-in would only test the error path.
/// </remarks>
sealed class StagedBuild : IDisposable {
    public StagedBuild() {
        Directory = System.IO.Directory.CreateTempSubdirectory("skala-build-").FullName;
        foreach (var file in System.IO.Directory.EnumerateFiles(AppContext.BaseDirectory, "Rikarin.Skala.*.dll")) {
            File.Copy(file, Path.Combine(Directory, Path.GetFileName(file)));
        }
    }

    public string Directory { get; }

    string Assembly(string name) => Path.Combine(Directory, name);

    /// <summary>A rebuild that changed something: one assembly's bytes are replaced by another's.</summary>
    public void Rebuild() =>
        File.Copy(Assembly("Rikarin.Skala.Core.dll"), Assembly("Rikarin.Skala.Formatting.CSharp.dll"), overwrite: true);

    /// <summary>
    ///     A rebuild that changed nothing: every file is rewritten with its own bytes, so lengths stay
    ///     put and last-write times move. ⚠ This is the case a timestamp-only check gets wrong, and
    ///     getting it wrong throws away a warm daemon on every no-op build.
    /// </summary>
    public void Touch() {
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.dll")) {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(1));
        }
    }

    public void Dispose() {
        try {
            System.IO.Directory.Delete(Directory, recursive: true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>
///     ⚠ <b>The regression test for the defect this whole mechanism exists for.</b>
/// </summary>
/// <remarks>
///     Before <see cref="BuildIdentity" />, a running daemon served output from the build it was
///     launched with for ever: <c>DaemonProtocol.Version</c> is a wire version and the wire shape does
///     not move when the formatter does, and <c>DaemonProtocol.IdleTimeout</c> is thirty minutes but
///     every request refreshes it. Rebuild the formatter, leave the daemon up, and <c>skala format</c>
///     kept producing the old bytes with no diagnostic anywhere — measured, twice in one day, at about
///     forty minutes of somebody's time each.
///     <para>
///         ⚠ The tests below run against a <i>staged</i> build directory rather than against the test
///         host's own assemblies, because a test cannot rewrite the assemblies its own process has
///         loaded. That is the honest limit of an in-process test: it proves the daemon notices its
///         install changing and refuses, not that the formatter's answer would have differed.
///         <c>StaleDaemonTests</c> in Rikarin.Skala.Cli.Tests is the end-to-end half, with a real daemon
///         process and a real install mutated underneath it.
///     </para>
/// </remarks>
public sealed class StaleBuildTests {
    [Fact]
    public async Task Format_AfterTheBuildChangesUnderneath_IsRefusedAndTheDaemonStops() {
        using var scratch = new Scratch();
        using var staged = new StagedBuild();
        var path = scratch.Write("A.cs", "class C{void M(){M();}}\n");

        await using var daemon = new Daemon(scratch.Root, new BuildIdentity(staged.Directory));
        daemon.Listen();
        using var stopping = new CancellationTokenSource();
        var running = daemon.RunAsync(stopping.Token);

        var served = DaemonClient.Send(scratch.Root, new DaemonRequest { Command = "format", Path = path });
        Assert.NotNull(served);
        Assert.True(served.Ok, served.Error);

        staged.Rebuild();

        var refused = DaemonClient.Send(scratch.Root, new DaemonRequest { Command = "format", Path = path });

        await stopping.CancelAsync();
        await running;

        // ⚠ This is the assertion that fails against the code before the fix: the daemon answered
        // `Ok = true` with the old build's `Formatted` text, for ever.
        Assert.NotNull(refused);
        Assert.False(refused.Ok, "the daemon served a format after its own build changed on disk");
        Assert.Null(refused.Formatted);
        Assert.Contains("stale daemon", refused.Error ?? string.Empty, StringComparison.Ordinal);
        Assert.True(daemon.StoppedForStaleBuild, "the daemon refused the request but stayed up");
    }

    [Fact]
    public async Task Format_AfterTheBuildIsOnlyTouched_IsStillServed() {
        using var scratch = new Scratch();
        using var staged = new StagedBuild();
        var path = scratch.Write("B.cs", "class C{void M(){M();}}\n");

        await using var daemon = new Daemon(scratch.Root, new BuildIdentity(staged.Directory));
        daemon.Listen();
        using var stopping = new CancellationTokenSource();
        var running = daemon.RunAsync(stopping.Token);

        DaemonClient.Send(scratch.Root, new DaemonRequest { Command = "format", Path = path });
        staged.Touch();
        var response = DaemonClient.Send(scratch.Root, new DaemonRequest { Command = "format", Path = path });

        await stopping.CancelAsync();
        await running;

        // ⚠ The reason the check reads MVIDs and not timestamps. A copy bumps the last-write time of
        // byte-identical assemblies, and a daemon that exits on that throws its warm cache away after
        // every build that changed nothing — trading a correctness defect for a performance one.
        Assert.NotNull(response);
        Assert.True(response.Ok, response.Error);
        Assert.False(daemon.StoppedForStaleBuild, "a touched but unchanged build stopped the daemon");
    }

    [Fact]
    public async Task Status_NamesTheBuildAndFlagsAStaleOne_WithoutStoppingIt() {
        using var scratch = new Scratch();
        using var staged = new StagedBuild();
        var identity = new BuildIdentity(staged.Directory);

        await using var daemon = new Daemon(scratch.Root, identity);
        daemon.Listen();
        using var stopping = new CancellationTokenSource();
        var running = daemon.RunAsync(stopping.Token);

        var fresh = DaemonClient.Send(scratch.Root, new DaemonRequest { Command = "status" });
        staged.Rebuild();
        var stale = DaemonClient.Send(scratch.Root, new DaemonRequest { Command = "status" });

        await stopping.CancelAsync();
        await running;

        // The cheapest half of the fix and the one a person uses: `daemon status` said nothing about
        // the build at all, so "this daemon is old" was never a hypothesis anybody could check.
        Assert.Contains("build " + identity.Loaded, fresh?.Status ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("STALE", fresh?.Status ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("STALE", stale?.Status ?? string.Empty, StringComparison.Ordinal);

        // ⚠ Reporting a stale daemon must not be what kills it, or the one command a person runs to
        // see the problem is the one command that hides it.
        Assert.False(daemon.StoppedForStaleBuild, "`daemon status` stopped the daemon it was reporting on");
    }

    [Fact]
    public void Identity_OfADirectoryWithNoAssemblies_NeverFires() {
        using var empty = new Scratch();
        var identity = new BuildIdentity(empty.Root);

        // ⚠ No baseline, no verdict. A check that fires when it cannot read the install would kill
        // daemons on layouts nobody anticipated, and the daemon is an optimisation.
        Assert.False(identity.Known);
        Assert.Equal(BuildIdentity.Unknown, identity.Loaded);
        Assert.False(identity.HasChanged());
    }
}
