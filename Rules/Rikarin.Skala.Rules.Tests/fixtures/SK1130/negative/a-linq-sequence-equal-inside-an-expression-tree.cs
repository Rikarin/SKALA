using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

// ⚠ The tripwire for #350, and it is deliberately a shape the rule does *not* match today. SK1130's
// rewrite is `receiver is "abc"` — a **constant pattern**, and a constant pattern inside a lambda
// converted to `Expression<TDelegate>` is CS8122. Nothing in the rule's path guards against that;
// what makes the hole unreachable is that the receiver must be `Span<char>`/`ReadOnlySpan<char>`,
// and a `ref struct` cannot appear in an expression tree at all — CS8640, compiled and confirmed,
// on the *unrewritten* source, so the rule is never even asked.
//
// ⚠ **The obvious next step for this rule is `IEnumerable<T>`**, which is exactly this file. Widen
// it here and the receiver constraint that was doing the safety work is gone, with no test failing
// — unless this one is present, in which case widening turns it red at the moment the guard becomes
// required. Verified by actually widening the rule: with the receiver check removed and
// `System.Linq.Enumerable` accepted alongside `System.MemoryExtensions`, this fixture goes red.
//
// ⚠ **The `&& items.Count > 0` is load-bearing and the first draft of this file did not have it.**
// A lambda body is not on `PatternSafety.IsPatternSafeContext`'s list, so with the call standing
// alone as the whole body the rule declines it for a reason that has nothing to do with the
// receiver — and the widening sabotage left the file green, which is a tripwire that does not trip.
// `&&` *is* on the list and is legal inside an expression tree, so the position stops being the
// reason and the receiver becomes the only one left.
//
// `ExpressionTreeGuardTests` holds the other half: the CS8640 that makes the span shape
// unreachable, and the CS8122 that would make the widened one broken.
public static class LinqSequenceEqualInsideAnExpressionTree {
    public static Expression<Func<List<char>, bool>> Filter() =>
        items => items.SequenceEqual("abc") && items.Count > 0;
}
