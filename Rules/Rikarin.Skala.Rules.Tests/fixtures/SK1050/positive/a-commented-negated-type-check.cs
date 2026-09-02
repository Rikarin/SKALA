// ⚠ #302's shape (#325), on this rule's negated-type-check branch — a third call site of the same
// guard in one analyzer, each moved separately because each protects a different edit. The comment
// is leading trivia of the `!`; the fix rewrites only `!(value is string)`.
public sealed class Inspector {
    public bool NotAString(object value) =>
        // the question is the type, and the negation is how it is asked
        !(value is string);
}
