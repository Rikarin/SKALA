using System;
using System.IO;
using System.Linq.Expressions;

// ⚠ The nested shape, kept separate because it is the one a nearest-lambda check gets wrong. The
// inner lambda's own `ConvertedType` is `Func<object, bool>` — not an `Expression` at all — and yet
// the whole tree is compiled as data, so a *pattern* written here is still CS8122 (measured in
// #347). A type test is not a pattern, so this compiles before and after the fix, and the round
// trip proves it rather than a comment claiming it.
public static class NestedInAnExpressionTree {
    public static Expression<Func<object, bool>> Filter() =>
        x => Apply(x, y => typeof(Stream).IsInstanceOfType(y));

    public static bool Apply(object value, Func<object, bool> predicate) => predicate(value);
}
