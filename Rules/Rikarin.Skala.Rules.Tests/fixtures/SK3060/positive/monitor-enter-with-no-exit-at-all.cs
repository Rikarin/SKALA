using System.Threading;

public sealed class Sequencer {
    readonly object gate = new();

    int issued;

    public int Next() {
        // No `Exit` in the method and none anywhere in the type, so the type-level escape does not
        // apply either. This is the other message: nothing to point the reader at, the lock is simply
        // taken and kept.
        Monitor.Enter(gate);
        issued++;

        return issued;
    }
}
