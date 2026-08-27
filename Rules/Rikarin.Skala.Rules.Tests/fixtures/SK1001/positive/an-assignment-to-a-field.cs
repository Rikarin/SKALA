using System.Collections.Generic;

public sealed class Names {
    List<string> _names = [];

    public void Reset() {
        _names = new List<string> { "a" };
    }
}
