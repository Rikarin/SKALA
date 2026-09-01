using System;

public sealed class Scheduler {
    readonly object queue = new();

    readonly object worker = new();

    int pending;

    public Action Enqueue() {
        // ⚠ The delegate is *written* inside `lock (queue)` and does not run inside it. Treating
        // the enclosing lock as held would invent the order `queue` → `worker`, and the invented
        // pair is what would produce the finding against `Run` below.
        lock (queue) {
            return () => {
                lock (worker) {
                    pending++;
                }
            };
        }
    }

    public void Run() {
        lock (worker) {
            lock (queue) {
                pending--;
            }
        }
    }
}
