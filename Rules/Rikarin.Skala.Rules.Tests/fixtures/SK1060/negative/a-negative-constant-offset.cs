using System.Collections.Generic;

// `^-1` does not reach the indexer at all: `Index`'s constructor throws ArgumentOutOfRangeException
// where the subtraction would have thrown IndexOutOfRangeException.
public sealed class Backwards {
    const int Back = -1;

    public string Beyond(List<string> items) => items[items.Count - Back];
}
