using System.Threading;

public sealed class Pipeline {
    readonly object gate = new();

    int processed;

    public void Run() {
        // The release is exception-safe and it is not lexically inside the `finally` — a local
        // function holds it and the `finally` calls that. Following the call would mean deciding, for
        // every delegate and every local function, whether it is invoked on this path, and the wrong
        // answer is a finding on code that is correct. The rule declines instead.
        Monitor.Enter(gate);
        try {
            processed = checked(processed + 1);
        } finally {
            Unlock();
        }

        void Unlock() => Monitor.Exit(gate);
    }
}
