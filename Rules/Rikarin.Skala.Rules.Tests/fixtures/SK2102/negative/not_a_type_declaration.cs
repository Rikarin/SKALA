using System.Diagnostics;

// `DebuggerDisplay` also targets a field and a property, and there the expression binds against
// the member's type rather than the annotated declaration. Nothing can be proved without deciding
// which, so only type declarations are examined.
sealed class Holder {
    [DebuggerDisplay("{Missing}")]
    public string Value = "";

    [DebuggerDisplay("{Missing}")]
    public int Count => 0;
}
