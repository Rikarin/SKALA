using System.Collections.Generic;

// The assignment target is not a plain name, so the rule has no written type it can read off it
// without evaluating something.
public sealed class Names {
    readonly List<string[]> _rows = [];

    public void Reset(int index) {
        _rows[index] = new string[] { "a" };
    }
}
