public sealed class Routing {
    static int Slow(int key) => key + 1;

    // A `switch` section holds a statement list and is not a block, so the adjacency question is
    // asked about the section rather than about an enclosing block.
    public static int Route(int key) {
        switch (key) {
            case 0:
                var slow = Slow(key);
                return slow;

            default:
                return 0;
        }
    }
}
