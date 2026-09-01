class Node {
    public bool Ready;
}

class C {
    void M(Node? node) {
        if (node != null & node.Ready) {
            Use(node);
        }
    }

    static void Use(Node node) { }
}
