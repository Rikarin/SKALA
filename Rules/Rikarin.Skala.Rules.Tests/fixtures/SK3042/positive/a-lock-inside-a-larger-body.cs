using System;

public sealed class Cache {
    readonly object gate = new();

    string? loaded;

    public string Load() {
        if (loaded == null) {
            Console.WriteLine("cold");
            lock (gate) {
                if (loaded == null) {
                    loaded = "value";
                }
            }
        }

        return loaded;
    }
}
