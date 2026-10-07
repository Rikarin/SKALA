// ⚠ #425: `Items[Items.Count - 1]` runs the getter twice and `Items[^1]` once, and this getter hands
// out a longer list each time it runs — #423's evaluation-count question, asked here as there.
using System.Collections.Generic;

public sealed class Source {
    readonly List<int> items = new() { 1 };

    public List<int> Items {
        get {
            items.Add(items.Count + 1);
            return items;
        }
    }
}

public static class Probe {
    public static string Run() {
        var source = new Source();
        try {
            return source.Items[source.Items.Count - 1].ToString();
        } catch (System.ArgumentOutOfRangeException) {
            return "out of range";
        }
    }
}
