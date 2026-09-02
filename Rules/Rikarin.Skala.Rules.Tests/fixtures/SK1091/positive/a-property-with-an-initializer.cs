using System.Collections.Generic;

public sealed class Cache {
    private Dictionary<string, int> Entries { get; set; } = new Dictionary<string, int>();

    public int Count() => Entries.Count;

    public void Reset() {
        Entries = new Dictionary<string, int>();
    }
}
