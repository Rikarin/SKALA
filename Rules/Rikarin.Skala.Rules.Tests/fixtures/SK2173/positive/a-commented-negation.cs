// ⚠ #302's shape (#325). The guard asked over the `not` pattern's FULL span, which begins at the
// leading trivia of the `not` keyword — so this comment declined the finding, while the fix only
// ever rewrites `not { }` itself and would have left the comment exactly where it is.
class C {
    bool M(object? result) =>
        result is
            // the negation is the point here: any instance at all passes
            not { };
}
