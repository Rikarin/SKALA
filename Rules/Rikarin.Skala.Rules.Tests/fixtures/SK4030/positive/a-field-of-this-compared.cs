// A field of `this` runs nothing and cannot throw, so reading it once instead of once per element is
// unobservable; the fix's result is pinned by Probe (#423).
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    readonly int wanted = 2;

    public bool Knows(List<int> ids) => ids.Any(id => id == this.wanted);
}

public static class Probe {
    public static bool Run() => new Registry().Knows(new List<int> { 1, 2, 3 });
}
