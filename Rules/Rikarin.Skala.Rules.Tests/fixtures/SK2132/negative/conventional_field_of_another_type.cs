using System.Collections.Generic;

// ⚠ `names` is spelled like `Names`'s field and is not of its type, so it is not this property's own
// field and the property is not following the convention here at all. Without the type test the rule
// would call `names` the repair and propose text that does not compile.
sealed class Roster {
    readonly List<string> names = new();
    string label = "";

    public string Names {
        get => label;
        set => label = value;
    }

    public int Size => names.Count;
}
