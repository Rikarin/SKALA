using System.Collections.Generic;

public sealed class Tags {
    readonly HashSet<string> items = [];

    public void UnionWith(Tags other) => items.UnionWith(other.items);
}

public sealed class Use {
    // A member of somebody else's type named `UnionWith` means whatever its author wrote.
    public static void Apply(Tags tags) => tags.UnionWith(tags);
}
