using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<int> entries = new();

    // ⚠ A computed property. The operators already admit the work, the source is not a path of
    // names, and the fix would have to leave a pipeline whose type is not the property's.
    public IReadOnlyList<int> Positive => entries.Where(static entry => entry > 0).ToList();
}
