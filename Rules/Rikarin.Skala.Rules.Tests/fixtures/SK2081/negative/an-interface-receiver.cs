using System.Collections.Generic;

public sealed class Sync {
    // The receiver table holds concrete framework collections. An `ISet<T>` is somebody's
    // implementation, and the rule does not extend a promise it did not read.
    public static void Prune(ISet<string> stale) {
        stale.ExceptWith(stale);
    }
}
