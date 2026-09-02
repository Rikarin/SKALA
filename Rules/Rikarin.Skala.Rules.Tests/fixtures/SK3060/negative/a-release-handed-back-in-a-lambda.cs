using System;
using System.Threading;

public sealed class Handle {
    readonly object gate = new();

    int version;

    public Action Begin() {
        // The lock is taken here and the release is handed to the caller as a delegate. Where it runs
        // is not knowable from this file — that is the contract, not an oversight — so a delegate
        // holding the release silences the rule rather than counting as a release on this path.
        Monitor.Enter(gate);
        version = checked(version + 1);

        return () => Monitor.Exit(gate);
    }
}
