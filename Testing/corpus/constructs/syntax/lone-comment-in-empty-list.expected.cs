// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A block comment spanning lines that is all an empty argument or parameter list holds (#509,
// SK-DIV-0209): the oracle writes `Foo(` / the comment at column 0, its other lines moved with its line /
// `)` on the opener's level — for a call, an object creation, a constructor initializer and a method's
// or a constructor's parameters. A `/** */` comment takes the list's level instead, a lambda's parameter
// list keeps the comment after its `(` and moves only the `)`, and a comment beside an argument stays.

namespace P;

public class T {
    public T() { }
}

public class C {
    void Foo() { }
    void Foo(int x) { }

    void M(int x) {
        Foo(
/* a
   b */
        );
        var t = new T(
/* a
   b */
        );
        Foo(
            /** a
               b */
        );
        System.Action f = ( /* a
           b */
        ) => { };
        Foo( /* a
           b */ x
        );
        Foo(
            x /* a
               b */
        );
    }

    void N(
/* a
       b */
    ) { }

    C(
/* a
   b */
    ) : this(
        1 /* c
        d */
    ) { }

    C(int x) { }
}
