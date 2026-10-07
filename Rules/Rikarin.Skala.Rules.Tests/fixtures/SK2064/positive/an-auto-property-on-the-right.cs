// An auto-property's getter runs nothing, so skipping it is unobservable. The fix stays unsafe all
// the same: where the right operand throws, `&&` stops the throw, and that is the finding (#423).
public sealed class Node {
    public bool Ready { get; set; }
}

public static class Probe {
    static bool Go(bool armed, Node node) => armed & node.Ready;

    public static int Run() => Go(true, new Node { Ready = true }) ? 1 : 0;
}
