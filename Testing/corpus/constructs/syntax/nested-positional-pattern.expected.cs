// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A positional pattern or a deconstruction designation broken inside a nested one (#473, SK-DIV-0114):
// the oracle spends one level for the whole pattern, not one per parenthesis — `o is (1` / `, (2` /
// `, 3))` puts `, 3` under `, (2`, three deep, through a recursive pattern's type and with a nested `)`
// on its own line alike — and none at all directly in an aligned statement condition. A tuple
// expression is the control: it spends one per parenthesis on both sides.

namespace P;

public class C {
    bool B(object o) =>
        o is (1
            , (2
            , 3));

    bool D(object o) =>
        o is (1
            , (2
            , (3
            , 4)));

    bool E(object o) =>
        o is (1
            , P(2
            , 3));

    bool F(object o) =>
        o is (1
            , (2
            , 3
            )
            , 4);

    bool H(object o) =>
        o is (1, (2
            , 3));

    void M(object o, (int, (int, int)) t, (int, (int, (int, int))) u) {
        var (a
            , (b
            , c)) = t;
        var (a1
            , (b1
            , (c1
            , d1))) = u;
        if (o is (1
            , (2
            , 3))) { }

        var t2 = (1
            , (2
                , 3), 4);
        var t3 = (1
            , (2
                , (3
                    , 4)));
    }
}
