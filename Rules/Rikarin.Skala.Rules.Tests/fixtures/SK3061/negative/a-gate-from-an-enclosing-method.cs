using System;

public sealed class Dispatcher {
    public Action Build() {
        var gate = new object();
        var count = 0;

        // ⚠ The declaration is in `Build` and the only `lock` is in the delegate, so the object is
        // created once per *call to `Build`* and shared by every invocation of the returned
        // `Action`. That is a working lock, and it is the exact inverse of shape 1 — which is why
        // the declaration must sit in the same function body as the `lock` rather than merely
        // somewhere above it.
        return () => {
            lock (gate) {
                count++;
            }
        };
    }
}
