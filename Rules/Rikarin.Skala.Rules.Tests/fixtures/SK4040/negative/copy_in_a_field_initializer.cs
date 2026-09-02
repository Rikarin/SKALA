using System.Collections.Generic;
using System.Linq;

public sealed class Snapshot {
    readonly List<string> source = new();

    // ⚠ One copy at construction, not one per read. The cost the rule is about is the frequency.
    readonly IReadOnlyList<string> taken;

    public Snapshot() => taken = source.ToArray();

    public IReadOnlyList<string> Taken => taken;
}
