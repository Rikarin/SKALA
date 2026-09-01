class Node {
    public bool Ready;
}

class C {
    bool M(Node? node) => node != null && node.Ready;

    bool N(string? text) => text is null || text.Length == 0;
}
