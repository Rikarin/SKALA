// ⚠ #302's shape, surviving in the shared helper #302 did not touch. `PrivateFieldUsage.TryRead`
// asked `ContainsCommentOrDirective(declaration)` over the field's FULL span, so the doc comment
// written ABOVE the field declined the finding — text this fix never touches, since its two edits
// are the type name and the initializer, both strictly inside the declaration's own span.
//
// ⚠ #302's table listed `SearchValuesAnalyzer` and moved its `invocation` guard onto the span
// overload. The guard that actually silenced it was one call deeper, in the helper three rules
// share, and moving the visible one left the rule exactly as dead on documented code.
using System;

class C {
    /// <summary>The vowels a caller may search for.</summary>
    static readonly char[] chars = new[] { 'a', 'e', 'i', 'o', 'u' };

    int M(ReadOnlySpan<char> text) => text.IndexOfAny(chars);
}
