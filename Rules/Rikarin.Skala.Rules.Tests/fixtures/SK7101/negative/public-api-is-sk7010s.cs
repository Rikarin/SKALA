// The two rules partition one population. Everything here is publicly visible through its whole
// containing chain, so it belongs to `SK7010` and this rule must be silent about all of it.
public sealed class Archetype {
    public int Count { get; }

    public void Clear() { }
}
