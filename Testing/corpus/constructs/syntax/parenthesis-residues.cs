// The residues of #470–#509 (issues #532–#536): a positional pattern in a property pattern keeps
// `X: (2` on one line and its items on `X`'s column; an empty argument or parameter list holding only a
// comment puts a blank line before a comment already on its own line, and a comment beside its `(` that
// ends the line at column 0; a long qualified name in `nameof` breaks at the last dot that fits; a chain
// lifted by a binary lifted by a grouping lifted by a binary; and an author's break after a member
// access's dot is joined, while one after a comment there is kept.
namespace P;

public class C {
    bool B(object o) => o is (1, { X: (2
, 3) });
    bool D(object o) => o is { X: (2
, 3) };
    bool E(object o) => o is { X: (2
, 3), Y: 1 };

    void M(C c, object a, object b, object x) {
        Foo(
            /* a */
        );
        Foo(
            // a
        );
        Foo(
            /* a
               b */
        );
        Foo(

            /* a
               b */
        );
        Foo(/* a */
        );
        Foo(
            /* a
               b */);
        Foo(
            /* a
               b */
            x
        );
        var n4 = nameof(a.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccccccccc.Dddddddddd);
        var e1 = c.
X;
        var e2 = c.
X();
        var e3 = c?.
X;
        var e4 = c.
X.
Y().
Z;
        System.
Console.WriteLine();
        var e5 = c
.X;
        var e6 = c. // note
X;
        var e7 = c./* c */
X;
        var e8 = a.B().
C().
D();
    }

    void N(
        // a
    ) { }

    void Foo() { }
    void Foo(object x) { }

    static bool Near(Vector2 point, float x, float y, float radius) =>
        ((point
.X
- x)
* (point.X - x))
+ ((point.Y - y) * (point.Y - y))
<= radius * radius;
}
