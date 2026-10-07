using System;

// ⚠ #302. The comment sits in the `catch` keyword's leading trivia, and `clause.Span` — the span the
// fix deletes — begins at that keyword. The first version of this rule read `DescendantTrivia`, which
// starts with the first token's leading trivia, and withdrew a correct finding to protect text the
// fix was never going to touch.
class C {
    static readonly int[] Values = new int[4];
    static int _closed;

    public static int Read(int index) {
        var value = 0;
        try {
            value = Values[index];
        }
        // Reviewed 2026-02: nothing to add here yet.
        catch (IndexOutOfRangeException) {
            throw;
        } finally {
            _closed++;
        }

        return value + _closed;
    }
}
