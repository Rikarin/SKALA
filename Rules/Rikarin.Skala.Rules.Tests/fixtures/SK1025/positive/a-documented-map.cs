// ⚠ #302's shape, reaching this rule through the helper the three private-field rewrites share.
// `PrivateFieldUsage.TryRead` asked the comment question over the field's FULL span, so a doc
// comment above the field silenced the rule — while this fix only ever rewrites the declared type
// and the object creation, both inside the declaration's own span.
using System.Collections.Generic;

class C {
    /// <summary>The lookup every request resolves against.</summary>
    static readonly Dictionary<string, int> map = new() { { "a", 1 }, { "b", 2 } };

    int M(string key) => map[key];
}
