using System;

// ⚠ Where it runs is not where it is written. A delegate handed to a scheduler is no longer
// instance code in the sense the finding means, so the rule declines rather than asserting
// something it cannot see.
sealed class Widget {
    static int created;

    public Action Bump() {
        return () => created++;
    }
}
