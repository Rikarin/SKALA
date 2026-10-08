// A list in a switch arm's `when` clause under an arrow the author put on a line of its own (#446,
// SK-DIV-0212): the arrow is kept, and the list nests from the arm's continuation line — the arguments
// two levels past the arm, `)` one level, `=> 1,` one level — whether the width chopped it (a `when`
// line of 121 and more) or the author did. A `when` line that fits stays whole. With the arrow written
// on the pattern's line the oracle never moves it, and the list nests one level in (not in this file:
// that is the ordinary rule, pinned elsewhere).
//
// ⚠ Not the `when xs.All(static e => e` / `is T` / `)` shape: the `)` follows this rule, but the oracle
// puts the `is` two levels past the arm and Skala one.
class C {
    int M(object o) {
        return o switch {
            // kept-arrow e=100
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=110
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=114
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=115
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=116
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=117
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=118
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=119
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=120
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=121
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=122
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow e=125
            X x when Compute(alpha, beta, gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                => 1,
            // kept-arrow-chopped e=110
            X x when Compute(
                alpha,
                beta,
                gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            )
                => 1,
            // kept-arrow-chopped e=117
            X x when Compute(
                alpha,
                beta,
                gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            )
                => 1,
            // kept-arrow-chopped e=121
            X x when Compute(
                alpha,
                beta,
                gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            )
                => 1,
            X x when Use(a, b
                + c)
                => 2,
            _ => 0
        };
    }
}
