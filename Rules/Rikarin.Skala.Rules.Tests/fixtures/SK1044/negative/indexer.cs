using System.Collections.Generic;

public sealed class Lookup {
    readonly Dictionary<string, string> entries = [];

    // The indexer runs twice in the long form and once inside the call.
    public bool IsBlank(string key) => entries[key] == null || entries[key].Length == 0;
}
