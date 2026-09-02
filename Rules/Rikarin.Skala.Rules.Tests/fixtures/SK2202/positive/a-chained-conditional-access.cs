using System;

public sealed class Node {
    public Action<int>? Sink { get; init; }
}

public sealed class Tree {
    int visited;

    public void Walk(Node? node) {
        node?.Sink?.Invoke(++visited);
    }
}
