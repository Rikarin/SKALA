using System.Collections.Generic;

// The receiver is called twice today. `^1` would call it once, which is a different program.
public sealed class Cache {
    List<string> Items() => new();

    public string Last() => Items()[Items().Count - 1];
}
