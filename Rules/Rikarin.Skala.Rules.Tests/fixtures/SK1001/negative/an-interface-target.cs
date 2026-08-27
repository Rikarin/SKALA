using System.Collections.Generic;

// `IList<string> x = […]` lets the compiler pick whatever implementation it likes. The object the
// code ends up holding is not the `List<string>` it holds today, and identity is observable.
public sealed class Names {
    public IList<string> All() {
        IList<string> names = new List<string> { "a", "b" };
        return names;
    }
}
