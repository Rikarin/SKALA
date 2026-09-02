using System;

class C {
    // ⚠ The mirror of `catch_with_a_comment_above_it`, and the pair is the point. There the fix
    // deletes `clause.Span` and the comment survives, so the finding stands. Here the `catch` is the
    // only clause, so the fix replaces the *whole* `try` with its block's contents — and the comment
    // between `}` and `catch` is inside what that replacement discards. One guard cannot answer both;
    // the unwrap asks about the header and the tail, which is where the loss actually is.
    public static void Save() {
        try {
            Run();
        }
        // Reviewed 2026-02: nothing to add here yet.
        catch (InvalidOperationException) {
            throw;
        }
    }

    static void Run() { }
}
