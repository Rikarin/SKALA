// ⚠ #431: the base constructor stores `this` where a static getter can reach it, and the derived
// constructor reads that getter before the overwrite. No instance member is named anywhere in the
// constructor. Measured `4` before the fix and `0` after. Two guards decline it, and each was
// sabotaged alone and then together: the getter runs code before the write, and with `this` leaked
// anything before the write that may throw is observable — a getter may.
public class Node {
    public static Node? Last;

    protected Node() {
        Last = this;
    }
}

public sealed class Leaf : Node {
    int weight = 4;

    public int Seen { get; }

    public Leaf() {
        Seen = Peeked;
        weight = 1;
    }

    static int Peeked => ((Leaf)Last!).weight;

    public int Weight => weight;
}

public static class Probe {
    public static int Run() => new Leaf().Seen;
}
