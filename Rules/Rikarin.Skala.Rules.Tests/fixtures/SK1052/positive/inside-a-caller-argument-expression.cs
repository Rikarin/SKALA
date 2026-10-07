// #422: the argument's source text is what the caller-argument parameter receives, so rewriting it to
// `node?.Label` there would change what Probe.Run() returns. The host withholds the safe mark here.
using System.Runtime.CompilerServices;

public sealed class Node {
    public string Label = "label";
}

public static class Probe {
    static string Check(object? value, [CallerArgumentExpression("value")] string text = "") => text;

    public static string Run() {
        Node? node = new Node();
        return Check(node != null ? node.Label : null);
    }
}
