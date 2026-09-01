using System.Collections.Generic;

public sealed class Node {
    public readonly HashSet<string> Items = [];
}

public sealed class Merge {
    // ⚠ `Items` is one field symbol and these are two objects. A rule that compared member names —
    // or symbols without the receivers — would empty `left` here and call it a finding.
    public static void Apply(Node left, Node right) {
        left.Items.UnionWith(right.Items);
    }
}
