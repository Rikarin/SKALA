// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
using System.Collections.Generic;

namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). A deconstruction's parenthesised designation is filled exactly as a
// tuple's components are: a kept break after a comma, before a comma or after the `(` comes back
// as written with the next variable one level in — under `foreach (` on the condition's aligned
// column — a designation past the margin fills at its commas, and a nested designation with a kept
// break inside keeps its head on the outer one's line. Before this file the designation had no
// plan at all, so a kept break was left as written and an overflowing one was never wrapped.
public class VariableDesignation {
    void AfterComma() {
        var (a,
            b) = (1, 2);
    }

    void AfterOpen() {
        var (
            a, b) = (1, 2);
    }

    void BeforeComma() {
        var (a
            , b) = (1, 2);
    }

    void Foreach(List<(int, int)> xs, List<(int, int, int, int)> wide) {
        foreach (var (a,
                     b) in xs) { }

        foreach (var (someVeryLongVariableNameNumberOne, someVeryLongVariableNameNumberTwo,
                     someVeryLongVariableNameNumberThree, x) in wide) { }
    }

    bool Pattern(object o) =>
        o is var (a,
            b);

    void Nested() {
        var (a2, (b2,
            c2)) = (1, (2, 3));
    }
}
