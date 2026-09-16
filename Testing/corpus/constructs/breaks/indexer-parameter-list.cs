namespace Constructs.Breaks;

// SK-DIV-0108 (issue #370). An indexer's bracketed parameter list is laid out with the declaration
// keys, exactly as a method's parenthesised one: a kept break after a default's `=` or after a comma
// chops the list, `]` takes a line of its own and the arrow after it breaks; a kept delimiter break
// chops it too; a break before a comma is joined; a list that overflows the margin chops. Before
// this file the list had no plan at all, so every one of these came back as written.
public class IndexerParameterList {
    int this[int a =
        5] => a;

    int this[int a
        , string b] => a;

    int this[int a,
        string b, object c] => a;

    int this[
        long a] => 0;

    int this[long a
        ] => 0;

    int this[int a, int b, string s, object o, long l, double d, float f, decimal m, byte by, char ch, short sh, ulong ul] => a;

    int this[int a, int b] => a;

    int this[string a, string b] {
        get => 0;
        set { }
    }

    int this[object a =
        null, string b = null] {
        get => 0;
    }
}

public class MethodTwin {
    int M(int a =
        5) => a;

    int N(int a
        , string b) => a;
}
