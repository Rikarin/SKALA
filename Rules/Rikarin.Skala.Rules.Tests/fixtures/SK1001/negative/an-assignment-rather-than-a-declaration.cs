using System.Collections.Generic;

// The rule reads the declared type beside the initializer. An assignment to an existing variable is
// a different question, and `[…]` there depends on a type written somewhere else in the file.
public sealed class Names {
    List<string> _names = [];

    public void Reset() {
        _names = new List<string> { "a" };
    }
}
