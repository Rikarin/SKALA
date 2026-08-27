// ⚠ "counts only members that are publicly visible through their whole containing chain" —
// rules.json, SK7010. Nothing here is reachable from outside the assembly, so nothing here is a
// documentation gap.
internal sealed class Internal {
    public int Value { get; set; }

    public void Run() { }

    internal sealed class Nested {
        public int Inner { get; set; }
    }
}

/// <summary>The one type here that is public, and therefore the one that is documented.</summary>
public sealed class PublicWithPrivateParts {
    /// <summary>Documented, because it is the public surface.</summary>
    public int Value { get; set; }

    int hidden;

    void Run() => hidden++;

    private sealed class Helper {
        public int Inner { get; set; }
    }
}
