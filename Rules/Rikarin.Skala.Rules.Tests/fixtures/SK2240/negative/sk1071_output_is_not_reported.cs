namespace Fixtures.SK2240;

public sealed record Triple(int A, int B, int C);

public static class Sk1071OutputIsNotReported {
    // ⚠ The other half of the disjointness, taken from SK1071's own fix output. SK1071 rewrites
    // `new Triple(t.A, t.B, c)` into exactly this, and its guard requires at least one member to be
    // carried across — so what it emits always assigns *fewer* than all the members, which this rule
    // declines. The two rules cannot hand work back to each other.
    public static Triple Replace(Triple t, int c) => t with { C = c };
}
