using System.Collections.Generic;
using System.Linq;

public sealed class Expander {
    readonly List<string> _names = new List<string>();

    public void Expand() {
        foreach (var name in _names) {
            _names.Add(name + "-copy");
        }
    }
}
