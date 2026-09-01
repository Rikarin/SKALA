using System.Collections.Generic;
using System.Linq;

namespace Contoso.Design;

// The repair, as a fixture. Every one of these keeps the promise the return type makes, and the rule
// has to stay silent on all of them or the fix it offers would be reported again after being applied.
public sealed class Orders {
    public IReadOnlyList<string> Pending(bool closed) => closed ? [] : new List<string>();

    public string[] Tags(bool closed) => closed ? [] : new string[1];

    public IEnumerable<string> Names(bool closed) => closed ? Enumerable.Empty<string>() : ["a"];
}
