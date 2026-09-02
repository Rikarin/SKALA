public sealed class WiderWrittenType {
    // The initializer position is whitelisted; the type written in it still has to be the one the
    // creation makes. `object?` is not `int?`, and what is stored is a box either way — a question
    // this rule declines to answer rather than answers.
    readonly object? boxed = new int?(5);

    public bool HasValue() => boxed is not null;
}
