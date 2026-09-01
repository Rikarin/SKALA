using System.Collections.Generic;

namespace Contoso.Design;

// A public mutable field is its own complaint and a different one. This rule is about the modifier
// that claims the field is safe; without it, nothing has been claimed.
public sealed class Loose {
    public string[] Names = ["red"];

    public List<int> Weights = [1];
}
