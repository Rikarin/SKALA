// A closing delimiter the author kept on a line of its own (#472, #505, #506; SK-DIV-0114, SK-DIV-0204).
// A grouping parenthesis's or a tuple's `)` is a continuation line of the statement: `return (a` /
// `    );` and `(a` / `    ).B();` take one level, under an arrow that already broke none, and a switch
// governed by it keeps it one level past the `(`'s line instead — under an arrow and in an argument
// list too — and nests its arms from that line. A list the oracle only fills — a positional pattern, a
// designation, a tuple type, a function pointer's parameters — keeps its closer one level past the
// opener's line wherever that line sits. An argument list's `)` is the control that goes back to its
// opener.
namespace P;

public unsafe class C {
    object M1() {
        (a
        ).B();
        var p = (a + b
        );
        return (a
        );
    }
    object M2() {
        Call(1, (a + b
        ));
        Call(1, (a, b
        ));
        var q =
            (a + b
            );
        var r =
            (a, b
            );
        return (1, 2
        );
    }
    object A() =>
        (a + b
        );
    object B() =>
        (a, b
        );
    bool P(object o) => o is (1, 2
    );
    bool Q(object o) =>
        o is (1, 2
        );
    (int a, int b
    ) M() => default;
    delegate*<int, void
    > F;
    void S() {
        var t4 = (1, 2
        ) switch {
            _ => 0
        };
        var t5 = (a
        ) switch {
            _ => 0
        };
        var t6 = Call(a
        ) switch {
            _ => 0
        };
        var t7 = Call(a,
            b
        ) switch {
            _ => 0
        };
        var (x, y
        ) = t;
        foreach (var (k, v
        ) in d) { }
        X((a + b
        ));
        if ((a
        ) == b) { }
        var w = (a + b
        ).C();
    }
    object R() {
        return (1, 2
        ) switch {
            _ => 0
        };
    }
    object T() {
        M((1, 2
        ) switch {
            _ => 0
        });
        t = (1, 2
        ) switch {
            _ => 0
        };
        return (1
        ) switch {
            _ => 0
        };
    }
    object U() =>
        (1, 2
        ) switch {
            _ => 0
        };
}
