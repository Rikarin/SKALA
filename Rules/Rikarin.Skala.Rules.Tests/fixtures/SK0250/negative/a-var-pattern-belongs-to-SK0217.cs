public static class Discarding {
    static bool TryRead(string text, out int value) {
        value = text.Length;
        return true;
    }

    // ⚠ `out var _` is not this rule's shape. It becomes `out _` under
    // `resharper_csharp_prefer_explicit_discard_declaration`, a tier-A option `SK0217` already
    // performs in both directions against the oracle — so reporting it here would be one edit
    // owned by two ids. It could not be reported anyway: a `var` pattern with a discard cannot
    // drop the designation, because `is var` is not a pattern.
    public static bool Any(string text) => TryRead(text, out var _);
}
