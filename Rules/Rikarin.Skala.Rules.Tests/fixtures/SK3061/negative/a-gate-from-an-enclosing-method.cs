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
        //
        // ⚠ Measured: deleting the `function != EnclosingFunction(declarator)` half of that gate
        // turns this fixture red, so it is a real gate. What does *not* turn it red is re-rooting
        // the capture walk at the declarator instead — then the closure check catches the same
        // reference and the finding is declined for the other reason. The two gates overlap on this
        // shape; only one of the two ways of breaking it is visible from here.
        return () => {
            lock (gate) {
                count++;
            }
        };
    }
}
