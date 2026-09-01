using System.Collections.Generic;

// The offset is evaluated once either way, but it is not a name path and the rule does not move
// anything it cannot read.
public sealed class Sliding {
    int Back() => 1;

    public string At(List<string> items) => items[items.Count - Back()];
}
