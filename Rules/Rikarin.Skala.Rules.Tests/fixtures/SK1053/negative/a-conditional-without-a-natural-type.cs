using System.Collections.Generic;

// `_ = flag ? 1 : null;` is CS0173: without the declaration the branches have no common type.
// `flag ? Make() : null` would compile, but a conditional is not an effect the rule keeps.
public sealed class Pool {
    static List<int> Make() => [];

    public void Warm(bool flag) {
        int? size = flag ? 1 : null;
        IList<int>? buffer = flag ? Make() : null;
    }
}
