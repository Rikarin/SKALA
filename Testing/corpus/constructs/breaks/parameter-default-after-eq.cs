namespace Constructs.Breaks;

// SK-DIV-0103 (issue #369). A break after the `=` of a parameter default is kept — a collection
// expression after it included — and the value takes a level of its own past the parameter. A
// parameter that spans lines makes the list chop, and a chopped list moves an expression body's
// arrow. The same `=` break is kept in a field, a property initializer and a local, and joined only
// when the bracket after it is itself broken, which puts the `[` on the `=`'s line.
public class ParameterDefaultAfterEq {
    void Collection(int[] a =
        [1, 2]) { }

    void Literal(int a =
        5) { }

    void Second(int a = 5, int b =
        6) { }

    void Both(object o =
        null, int[] xs =
        [3]) { }

    void BeforeTheEquals(int a
        = 5) { }

    int Arrow(int a =
        5) => a;

    ParameterDefaultAfterEq(int a =
        5) { }

    void ChoppedBracket(int[] a =
        [
            1, 2
        ]) { }

    int[] Field =
        [1, 2];

    int[] Property { get; } =
        [1, 2];

    void Locals() {
        int[] local =
            [1, 2];
        int[] chopped =
            [
                1, 2
            ];
        int[] empty =
            [];
        System.Func<int, int> lambda = (int a =
            5) => a;
        var initializer = new ParameterDefaultAfterEq { X =
            1, Y = 2 };
    }

    [System.Obsolete(Message =
        "x")]
    void Attributed() { }

    int X, Y;
}
