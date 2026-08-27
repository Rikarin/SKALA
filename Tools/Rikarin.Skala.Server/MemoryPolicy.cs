using System.Globalization;

namespace Rikarin.Skala.Server;

/// <summary>
///     What the daemon does when it is holding too much.
/// </summary>
/// <remarks>
///     docs/plan/13 § "Memory", in full and in order:
///     <list type="bullet">
///         <item>Parsed trees: LRU by content hash, capped at 400 MB, dropped first.</item>
///         <item>Compilations: at most 4 retained; the rest rebuilt on demand.</item>
///         <item>
///             On RSS above the cap: drop the tree cache, then compilations, then <b>exit rather than swap</b>.
///         </item>
///     </list>
///     <para>
///         ⚠ <b>The exit is the part that matters and the part that is easy to leave out.</b> A daemon that
///         pushes a laptop into swap is worse than no daemon: the machine becomes unusable, the cause is
///         invisible, and the blame lands on the editor. Exiting is always safe — every command works
///         identically with <c>SKALA_NO_DAEMON=1</c>, so a daemon that is gone costs the next invocation its
///         cold path and nothing else, and the lazy start brings a fresh one back.
///     </para>
///     <para>
///         ⚠ The trigger is <see cref="GCMemoryInfo" /> and the process's working set, polled, rather than
///         <c>GC.RegisterForFullGCNotification</c>: that API is only raised for blocking gen-2 collections
///         and the server GC configuration the daemon inherits mostly does not produce them, so the
///         notification that was supposed to be the signal never arrives. A poll every few seconds costs
///         nothing on a process that is idle by design.
///     </para>
/// </remarks>
public sealed class MemoryPolicy {
    /// <summary>Trees are dropped above this. docs/plan/13: "capped at 400 MB, dropped first".</summary>
    public long TreeCacheBytes { get; init; } = 400L * 1024 * 1024;

    /// <summary>
    ///     The whole process's ceiling. docs/plan/13's budget row is "Daemon RSS, idle after a corpus
    ///     run &lt; 1.5 GB"; this is the point at which the daemon starts giving things back, set below
    ///     the budget so that the budget is what is *observed* rather than what is aimed at.
    /// </summary>
    public long SoftLimitBytes { get; init; } = 1_200L * 1024 * 1024;

    /// <summary>
    ///     Above this, after dropping everything droppable, the daemon exits. ⚠ Deliberately below the
    ///     point at which a 16 GB laptop with an IDE and a browser open begins to swap.
    /// </summary>
    public long HardLimitBytes { get; init; } = 1_500L * 1024 * 1024;

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How many times the policy has had to intervene, for <c>daemon status</c>.</summary>
    public int Drops { get; private set; }

    public enum Action {
        /// <summary>Under the limit; nothing done.</summary>
        None,

        /// <summary>The tree cache was dropped. The cheapest thing to lose and the first to go.</summary>
        DroppedTrees,

        /// <summary>Trees and compilations both gone, and the process is still over.</summary>
        DroppedCompilations,

        /// <summary>⚠ Nothing left to give back. Exit rather than swap.</summary>
        Exit
    }

    /// <summary>
    ///     One step of the policy, given the current usage. Pure, so that it is testable without
    ///     allocating a gigabyte.
    /// </summary>
    /// <param name="workingSet">The process's resident set.</param>
    /// <param name="alreadyDroppedTrees">Whether this pass has already dropped the tree cache.</param>
    public Action Decide(long workingSet, bool alreadyDroppedTrees) {
        if (workingSet < SoftLimitBytes) {
            return Action.None;
        }

        if (!alreadyDroppedTrees) {
            return Action.DroppedTrees;
        }

        return workingSet >= HardLimitBytes ? Action.Exit : Action.DroppedCompilations;
    }

    /// <summary>
    ///     Polls until cancelled, applying the policy. Returns when the daemon should stop.
    /// </summary>
    /// <returns><see langword="true" /> when the caller must exit the process.</returns>
    public async Task<bool> WatchAsync(
        FormatService trees,
        RetainedCompilations compilations,
        CancellationToken cancellation
    ) {
        try {
            while (!cancellation.IsCancellationRequested) {
                await Task.Delay(Interval, cancellation).ConfigureAwait(false);

                if (trees.Bytes > TreeCacheBytes) {
                    // ⚠ The cheap case, and the common one: the cache's own bound was exceeded
                    // without the process being anywhere near its limit. Trim to the low-water mark
                    // rather than clearing — this is the LRU doing its job, not an emergency.
                    trees.Trim(TreeCacheBytes);
                }

                var workingSet = Environment.WorkingSet;
                if (workingSet < SoftLimitBytes) {
                    continue;
                }

                Drops++;
                trees.Clear();

                // ⚠ A blocking, compacting collection. Ordinarily a bad idea; here it is the whole
                // point — the question the next line asks is whether the memory can be *given back*,
                // and that cannot be answered without collecting first.
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();

                if (Environment.WorkingSet < SoftLimitBytes) {
                    continue;
                }

                compilations.Clear();
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

                var after = Environment.WorkingSet;
                if (after < HardLimitBytes) {
                    continue;
                }

                await Console.Error.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"skala daemon: holding {after / (1024 * 1024)} MB after dropping every cache, over the {HardLimitBytes / (1024 * 1024)} MB limit. Exiting rather than swapping; the next `skala` invocation starts a fresh one."
                    )
                )
                    .ConfigureAwait(false);

                return true;
            }
        } catch (OperationCanceledException) {
            // Shutting down.
        }

        return false;
    }
}
