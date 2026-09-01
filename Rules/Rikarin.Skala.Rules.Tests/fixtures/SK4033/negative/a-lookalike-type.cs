using System.Collections.Generic;

public sealed class Registry {
    public ICollection<string> Keys { get; } = new List<string>();
}

public sealed class Cache {
    public static int Size(Registry registry) => registry.Keys.Count;
}
