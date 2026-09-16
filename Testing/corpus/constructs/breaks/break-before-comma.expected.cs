// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-16
namespace Constructs.Breaks;

// SK-DIV-0104 (issue #369). Under wrap_before_comma = false, a break the author wrote before a
// comma is kept in a tuple and in a type parameter list — the constructs the oracle fills but never
// re-lays — and joined in every construct with a wrap style of its own: an argument list, a
// parameter list, an initializer, a collection expression, a switch expression's arms, an enum's
// members, a declaration's declarators, an attribute's arguments. A kept break makes the owner
// multi-line, so an arrow above it breaks.
public class BreakBeforeComma {
    object Tuple() =>
        ([1, 2]
            , 3);

    object TwoBreaks() =>
        (1
            , 2
            , 3);

    object Named() =>
        (a: 1
            , b: 2);

    object AfterTheComma() =>
        (1,
            2);

    object BothSides() =>
        (1
            ,
            2);

    int TypeParameters<T
        , U>() =>
        0;

    int TypeParametersAfterTheComma<T,
        U>() =>
        0;

    void Local() {
        var tuple = (1
            , 2);
    }

    void Joined(int a, int b) {
        F(1, 2);
        int[] collection = [1, 2];
        var array = new[] { 1, 2 };
        var initializer = new BreakBeforeComma { X = 1, Y = 2 };
        var anonymous = new { A = 1, B = 2 };
        var arms = (1, 2) switch {
            (1, _) => 1,
            _ => 2
        };
        int declared = 1, other = 2;
    }

    [System.Obsolete("x", true)]
    void F(int a, int b) { }

    int X, Y;
}

public enum Members {
    A,
    B
}
