using System;

// ⚠ #302. The comment sits in the `catch` keyword's leading trivia, and `clause.Span` — the span the
// fix deletes — begins at that keyword. The first version of this rule read `DescendantTrivia`, which
// starts with the first token's leading trivia, and withdrew a correct finding to protect text the
// fix was never going to touch.
class C {
    public static void Save() {
        try {
            Run();
        }
        // Reviewed 2026-02: nothing to add here yet.
        catch (InvalidOperationException) {
            throw;
        } finally {
            Close();
        }
    }

    static void Run() { }

    static void Close() { }
}
