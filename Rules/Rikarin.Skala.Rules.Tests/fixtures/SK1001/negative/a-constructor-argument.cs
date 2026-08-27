using System.Collections.Generic;

// The capacity is a decision about allocation that a collection expression does not preserve.
public sealed class Names {
    public List<string> All() {
        List<string> names = new List<string>(16) { "a", "b" };
        return names;
    }
}
