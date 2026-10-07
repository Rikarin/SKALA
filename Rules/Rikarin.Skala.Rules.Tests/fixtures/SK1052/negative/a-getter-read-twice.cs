// ⚠ #423: the test and the access read `Current` twice and `o.Current?.Label` reads it once.
// Measured for #412's audit with a getter that answers null the second time:
// `NullReferenceException` before the fix, the label after. A getter with a body is not storage.
public sealed class Node {
    public string Label = "label";
}

public sealed class Owner {
    public int Reads;

    public Node? Current {
        get {
            Reads++;
            return Reads == 1 ? new Node() : null;
        }
    }
}

public static class Probe {
    public static int Run() {
        var o = new Owner();
        var label = o.Current != null ? o.Current.Label : null;
        return o.Reads * 10 + (label == null ? 0 : label.Length);
    }
}
