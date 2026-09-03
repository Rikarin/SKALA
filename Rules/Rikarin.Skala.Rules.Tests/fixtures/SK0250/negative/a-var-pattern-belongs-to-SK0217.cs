public static class Discarding {
    static bool TryRead(string text, out int value) {
        value = text.Length;
        return true;
    }

    // ⚠ `out var _` is not this rule's shape. It becomes `out _` under
    // `skala_prefer_explicit_discard_declaration`, a tier-A option `SK0217` already
    // performs in both directions against the oracle — so reporting it here would be one edit
    // owned by two ids.
    //
    // ⚠ **This fixture is a boundary record and not a test, and that was measured.** Sabotaging
    // the rule to admit `var` patterns — in the registration and in the switch, both — leaves
    // this file green and turns only `a-var-pattern-under-is` red. `out var _` is a
    // *declaration expression* in an argument, not a pattern of any kind, so no pattern node
    // kind reaches it: the exclusion is structural rather than guarded. Kept because the next
    // person to widen the registration needs the reason written down where they will be.
    public static bool Any(string text) => TryRead(text, out var _);
}
