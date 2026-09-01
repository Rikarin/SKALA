using System.Collections.Generic;

// The declaration is not immediately above the call, so moving it moves it past something.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) {
        int value;
        System.Console.WriteLine(key);
        if (entries.TryGetValue(key, out value)) {
            return value;
        }

        return 0;
    }
}
