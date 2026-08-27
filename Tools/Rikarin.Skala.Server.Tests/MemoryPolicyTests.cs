using Rikarin.Skala.Protocol;
using Rikarin.Skala.Server;

namespace Rikarin.Skala.Server.Tests;

/// <summary>
///     docs/plan/13 § "Memory": drop trees, then compilations, then exit rather than swap.
/// </summary>
/// <remarks>
///     ⚠ The policy's decision function is pure so that this can be asserted without allocating a
///     gigabyte. A memory policy that can only be tested by exhausting the machine is a memory policy
///     that is tested once, by hand, before it is first shipped and never again.
/// </remarks>
public sealed class MemoryPolicyTests {
    static readonly MemoryPolicy Policy = new() { SoftLimitBytes = 1000, HardLimitBytes = 2000, TreeCacheBytes = 400 };

    [Fact]
    public void UnderTheSoftLimit_NothingIsDropped() =>
        Assert.Equal(MemoryPolicy.Action.None, Policy.Decide(999, alreadyDroppedTrees: false));

    [Fact]
    public void OverTheSoftLimit_TreesGoFirst() =>
        Assert.Equal(MemoryPolicy.Action.DroppedTrees, Policy.Decide(1000, alreadyDroppedTrees: false));

    [Fact]
    public void StillOverAfterDroppingTrees_CompilationsGoNext() =>
        Assert.Equal(MemoryPolicy.Action.DroppedCompilations, Policy.Decide(1500, alreadyDroppedTrees: true));

    /// <summary>
    ///     ⚠ The one that matters. Exiting is always safe — every command works identically with
    ///     <c>SKALA_NO_DAEMON=1</c> — and swapping never is.
    /// </summary>
    [Fact]
    public void OverTheHardLimitWithNothingLeftToDrop_TheDaemonExits() =>
        Assert.Equal(MemoryPolicy.Action.Exit, Policy.Decide(2000, alreadyDroppedTrees: true));

    [Fact]
    public void TheDefaultLimits_MatchTheBudgetInDoc13() {
        var defaults = new MemoryPolicy();

        // doc 13 § "Memory": "Parsed trees: LRU by content hash, capped at 400 MB".
        Assert.Equal(400L * 1024 * 1024, defaults.TreeCacheBytes);

        // doc 13 § "Budgets": "Daemon RSS, idle after a corpus run | < 1.5 GB". The daemon starts
        // giving memory back below that, so that the budget is observed rather than aimed at.
        Assert.True(defaults.SoftLimitBytes < defaults.HardLimitBytes);
        Assert.Equal(1_500L * 1024 * 1024, defaults.HardLimitBytes);
    }
}

/// <summary>docs/plan/13 § "Memory": "Compilations: at most 4 retained".</summary>
public sealed class RetainedCompilationsTests {
    [Fact]
    public void AtMostFourAreRetained() {
        var store = new RetainedCompilations();

        for (var i = 0; i < 20; i++) {
            store.Put("c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), new object());
        }

        Assert.Equal(4, store.Held);
        Assert.Equal(16, store.Evictions);
    }

    /// <summary>
    ///     ⚠ Least-recently-*used*, not least-recently-added. A compilation the caller keeps asking for
    ///     is the expensive one to rebuild, and evicting it because it happens to be the oldest
    ///     insertion is the failure that makes a cache slower than no cache.
    /// </summary>
    [Fact]
    public void TheEntryStillBeingUsed_SurvivesNewerOnes() {
        var store = new RetainedCompilations();
        var kept = new object();
        store.Put("kept", kept);

        for (var i = 0; i < 3; i++) {
            store.Put("filler" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), new object());
            Assert.True(store.TryGet("kept", out _));
        }

        store.Put("newcomer", new object());

        Assert.True(store.TryGet("kept", out var found));
        Assert.Same(kept, found);
        Assert.Equal(4, store.Held);
    }

    [Fact]
    public void Clear_DropsEverything() {
        var store = new RetainedCompilations();
        store.Put("a", new object());

        store.Clear();

        Assert.Equal(0, store.Held);
        Assert.False(store.TryGet("a", out _));
    }
}

/// <summary>
///     ⚠ The kernel caps a Unix domain socket path at 104 bytes. This was a live defect: a repository
///     nested deeper than about eighty-five characters made <c>Daemon.Listen</c> throw
///     <see cref="ArgumentOutOfRangeException" />, and the daemon died with an unhandled exception and
///     exit code 0 while every later format silently took the cold path.
/// </summary>
public sealed class SocketPathTests {
    [Fact]
    public void AShortRoot_KeepsTheSocketBesideTheRepository() {
        var path = DaemonProtocol.SocketPath(OperatingSystem.IsWindows() ? @"C:\r" : "/r");

        Assert.Contains(".skala", path, StringComparison.Ordinal);
        Assert.EndsWith("daemon.sock", path, StringComparison.Ordinal);
    }

    [Fact]
    public void ARootTooDeepForTheKernel_MovesTheSocketSomewhereThatFits() {
        var deep = "/" + string.Join("/", Enumerable.Repeat("a-fairly-long-directory-name", 8));

        var path = DaemonProtocol.SocketPath(deep);

        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(path) <= 104,
            $"the socket path is {path.Length} bytes and the kernel's cap is 104: {path}"
        );

        Assert.DoesNotContain(deep, path, StringComparison.Ordinal);
    }

    /// <summary>Two repositories must not share a daemon, however the path was shortened.</summary>
    [Fact]
    public void TwoDeepRoots_GetTwoDifferentSockets() {
        var a = "/" + string.Join("/", Enumerable.Repeat("a-fairly-long-directory-name", 8)) + "/one";
        var b = "/" + string.Join("/", Enumerable.Repeat("a-fairly-long-directory-name", 8)) + "/two";

        Assert.NotEqual(DaemonProtocol.SocketPath(a), DaemonProtocol.SocketPath(b));
    }

    [Fact]
    public void TheSameRoot_AlwaysGetsTheSameSocket() {
        var deep = "/" + string.Join("/", Enumerable.Repeat("a-fairly-long-directory-name", 8));

        Assert.Equal(DaemonProtocol.SocketPath(deep), DaemonProtocol.SocketPath(deep));
    }

    /// <summary>
    ///     ⚠ doc 12 § "Cross-platform" lists the named-pipe daemon transport as a Windows hazard. Before
    ///     M7 there was nothing to test: both ends built an AF_UNIX socket unconditionally and only a
    ///     comment claimed otherwise.
    /// </summary>
    [Fact]
    public void TheTransport_IsAPipeOnWindowsAndASocketElsewhere() =>
        Assert.Equal(OperatingSystem.IsWindows(), DaemonTransport.UsesNamedPipe);

    [Fact]
    public void APipeName_IsPerRepositoryAndPerUserAndCarriesNoSeparator() {
        var one = DaemonProtocol.PipeName(OperatingSystem.IsWindows() ? @"C:\a\one" : "/a/one");
        var two = DaemonProtocol.PipeName(OperatingSystem.IsWindows() ? @"C:\a\two" : "/a/two");

        Assert.NotEqual(one, two);
        Assert.DoesNotContain("\\", one, StringComparison.Ordinal);
        Assert.DoesNotContain("/", one, StringComparison.Ordinal);
        Assert.Contains(Environment.UserName, one, StringComparison.Ordinal);
    }
}
