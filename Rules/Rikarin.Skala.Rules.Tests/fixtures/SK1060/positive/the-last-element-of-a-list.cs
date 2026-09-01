using System.Collections.Generic;

public sealed class Stack {
    readonly List<string> items = new();

    public string Top() => items[items.Count - 1];
}
