using System.Collections.Generic;

public sealed class Sync {
    public static void Prune(HashSet<string> stale, HashSet<string> current) {
        stale.ExceptWith(current);
    }
}
