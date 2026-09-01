using System.Collections.Generic;

public sealed class Sync {
    public static void Prune(HashSet<string> stale) {
        stale.ExceptWith(stale);
    }
}
