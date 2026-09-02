// ⚠ The same defect as SK1023's `a-documented-field`, on a statement instead of a declaration: a
// comment written above an `if` is leading trivia of the `if` keyword, so it sits inside the node's
// FULL span and the old guard declined. The fix rewrites the chain from `if` to the last `else`
// body — `root.Span` — and never touches the line above it.
//
// A comment above a conditional is about as ordinary as C# gets, which is what made this rule dead
// on most real code while every one of its negatives kept passing.
class C {
    string M(string? x) {
        // Decides which label this value gets.
        if (x == null) {
            return "none";
        } else if (x == "a") {
            return "one";
        } else {
            return "other";
        }
    }
}
