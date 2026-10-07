// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0208 (issue #444). An array initializer's fill measures an element flat, the author's kept
// breaks read as spaces and a comment or literal that spans lines counted to its first line: it stays
// on the line when that fits and moves down when it does not, and the element after one that spanned
// lines starts a line of its own. A collection expression too long for any line keeps its `[` beside
// the element before it.

public class ArrayElementDraft1 {
    void M() {
        var a1 = new object[] {
            alphaValue, betaValue,
            Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilon), tail
        };
        var a2 = new object[] {
            alphaValue, betaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            tail
        };
        var a3 = new object[] {
            alphaValue, betaValue,
            new[] {
                alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,
                zeta
            },
            tail
        };
        var a4 = new object[] {
            alphaValue, betaValue,
            new[] {
                alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,
                zetaArgumentValue, eta
            },
            tail
        };
        var a5 = new object[] {
            alphaValue, betaValue,
            new List<int> {
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            },
            tail
        };
        var a6 = new object[] {
            alphaValue, betaValue,
            x => Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zeta
            ),
            tail
        };
        var a7 = new object[] {
            alphaValue, betaValue,
            alphaArgumentValue
            + betaArgumentValue
            + gammaArgumentValue
            + deltaArgumentValue
            + epsilonArgumentValue
            + zeta,
            tail
        };
        var a8 = new object[] {
            alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue),
            Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue), tail
        };
        var a9 = new object[] {
            alphaValue, betaValue, Compute(
                alphaArgumentValue,
                betaArgumentValue
            ),
            tail
        };
        var b1 = new object[] {
            alphaValue, betaValue, Compute(
                alphaArgumentValue,
                betaArgumentValue
            ),
            tail
        };
    }
}

public class ArrayElementDraft2 {
    void M() {
        var c1 = new object[] {
            alphaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            tail
        };
        var c2 = new object[] {
            alphaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            )
        };
        var c3 = new object[] {
            alphaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue)
        };
        var c4 = new object[] {
            alphaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest]
        };
        var c5 = new object[] {
            alphaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            new[] {
                "ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest
            }
        };
        var c6 = new object[] {
            alphaValue,
            Compute(
                alphaArgumentValue,
                betaArgumentValue,
                gammaArgumentValue,
                deltaArgumentValue,
                epsilonArgumentValue,
                zetaArgumentValue
            ),
            [1, 2]
        };
        var c7 = new object[] {
            alphaValue,
            new[] {
                alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,
                zetaArgumentValue, eta
            },
            ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest]
        };
        var c8 = new object[] {
            alphaValue, betaValue,
            ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest]
        };
    }
}

public class ArrayElementDraft3 {
    void M() {
        var b1 = new[] {
            1, Compute(
                2, /* a
                  b */
                3
            ),
            4
        };
        var b2 = new[] {
            new[] {
                1, 2 /* a
                  b */,
                3
            },
            new[] { 4 }
        };
        var b3 = new[] {
            first is string, second
                is string
        };
        var b4 = new[] { 1, Compute(2), 3, 4 };
        var b5 = new[] { 1, 2, Compute(2), 3, 4, 5 };
        var b6 = new[] { Compute(2), 3, 4 };
        int[] b7 = [1, Compute(2), 3];
    }

    void N() {
        var a = new Func<int>[] { () => 1, () => { return 2; }, () => 3 };
        var b = new[] { 1, 2, 3, 4 };
        var c = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } };
        var d = new[] {
            Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 2, 3
        };
        var e = new[] {
            1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 3
        };
        var g = new[] {
            1, x switch {
                1 => 2,
                _ => 3
            },
            4
        };
        var h = new object[] { 1, new { A = 1 }, 2 };
    }
}

public class ArrayElementDraft4 {
    void M() {
        var a = new Func<int>[] { () => 1, () => { return 2; }, () => 3 };
        var b = new[] { 1, 2, 3, 4 };
        var c = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } };
        var d = new[] {
            Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 2, 3
        };
        var e = new[] {
            1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 3
        };
        var f = new[] {
            "a", """
                 raw
                 """,
            "b"
        };
        var g = new[] {
            1, x switch {
                1 => 2,
                _ => 3
            },
            4
        };
        var h = new object[] { 1, new { A = 1 }, 2 };
    }
}
