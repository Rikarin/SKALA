using System.Collections.Generic;

public sealed class Buckets {
    // `groups[0]` twice is two indexer calls. The text matches and the storage is not proved.
    public static void Merge(List<HashSet<string>> groups) {
        groups[0].UnionWith(groups[0]);
    }
}
