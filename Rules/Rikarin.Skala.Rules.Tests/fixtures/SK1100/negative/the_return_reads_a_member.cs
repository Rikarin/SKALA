using System.Collections.Generic;

public sealed class Counting {
    static List<int> Build() => new();

    // `return items.Count;` is not the local handed back, it is a member read on it — a different
    // rewrite with a different question about evaluation.
    public static int Size() {
        var items = Build();
        return items.Count;
    }
}
