using System;

public sealed class Scheduler {
    int queued;

    public Action Prepare() {
        var gate = new object();

        // ⚠ The local is created here and would satisfy every other clause of shape 1 — but a
        // delegate closes over it below, so it outlives this call and "one monitor per invocation"
        // stops being true. Whether the two locks then exclude each other depends on who holds the
        // returned `Action` and when they invoke it, which is not knowable from the shape. Decline
        // rather than reason about it.
        lock (gate) {
            queued++;
        }

        return () => {
            lock (gate) {
                queued--;
            }
        };
    }
}
