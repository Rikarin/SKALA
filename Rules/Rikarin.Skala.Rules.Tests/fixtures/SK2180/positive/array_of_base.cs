class Node { }

sealed class Leaf : Node { }

static class Walk {
    public static int Count(Node[] nodes) {
        var total = 0;
        foreach (Leaf leaf in nodes) {
            total += leaf.GetHashCode();
        }

        return total;
    }
}
