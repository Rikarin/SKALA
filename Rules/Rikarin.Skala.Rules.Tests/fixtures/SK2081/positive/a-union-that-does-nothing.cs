using System.Collections.Generic;

public sealed class Sync {
    readonly HashSet<string> known = [];

    public void Merge() {
        known.UnionWith(this.known);
    }
}
