using System;

// ⚠ #327, and the lead was right: SK1130 fired here, and its fix produced `name is "abc";` —
// `CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be
// used as a statement`. `PatternSafety.IsPatternSafeContext` admitted `ExpressionStatementSyntax`,
// which answers the grammar question correctly and the language question wrongly: a pattern can
// never stand alone as a statement, whatever the parentheses say.
//
// ⚠ The entry was dead until SK1130 shipped. SK1050, the only other caller, rewrites a comparison,
// and a bare `x != null;` is already CS0201 before any rewrite — so the position was unreachable on
// code that compiles. SK1130 rewrites an *invocation*, and the call below is perfectly legal, which
// is what turned a harmless entry into a fix that does not compile.
//
// ⚠ Discarding SequenceEqual's result is pointless code that may exist in no real tree, and the
// corpus cannot speak to it either way: SK1130's zero-false-positive evidence is a superset argument
// over exactly two call sites in the reference trees. Low severity, not absent.
public static class Transforms {
    public static void Ignore(ReadOnlySpan<char> name) {
        name.SequenceEqual("abc");
    }
}
