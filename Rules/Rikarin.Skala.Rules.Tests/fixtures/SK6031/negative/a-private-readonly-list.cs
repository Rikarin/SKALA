using System.Collections.Generic;

namespace Contoso.Design;

// The type owns its own state; `readonly` here is a note to its own author, not a promise.
public sealed class Basket {
    readonly List<int> weights = [1, 2];

    private readonly string[] names = ["red"];

    public int Count => weights.Count + names.Length;
}
