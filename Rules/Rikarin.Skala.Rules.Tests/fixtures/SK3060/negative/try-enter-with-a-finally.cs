using System.Threading;

public sealed class Attempt {
    readonly object gate = new();

    int served;

    public bool Serve() {
        // `TryEnter` is the second `Monitor` row and it releases with `Exit`, not with a `TryExit`
        // that does not exist. The `if` guards the `finally` because a failed `TryEnter` took
        // nothing, which is the same reason the `lockTaken` idiom exists.
        if (!Monitor.TryEnter(gate, 100)) {
            return false;
        }

        try {
            served = checked(served + 1);
        } finally {
            Monitor.Exit(gate);
        }

        return true;
    }
}
