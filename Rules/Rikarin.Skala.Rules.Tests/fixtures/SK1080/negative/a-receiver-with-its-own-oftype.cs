// ⚠ #424: an instance method wins over an extension method, so `bag.OfType<string>()` is the bag's
// own (which yields nothing), not `Enumerable.OfType` (#412's audit: "a" became "").
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public sealed class Bag : IEnumerable<object> {
    public IEnumerable<T> OfType<T>() {
        yield break;
    }

    public IEnumerator<object> GetEnumerator() {
        yield return "a";
        yield return 1;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class Probe {
    public static string Run() => string.Join(",", new Bag().Where(x => x is string).Cast<string>());
}
