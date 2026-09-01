using System.Diagnostics.Contracts;

static class Parsing {
    [Pure]
    public static void Widen(ref int value) => value = value * 2;
}
