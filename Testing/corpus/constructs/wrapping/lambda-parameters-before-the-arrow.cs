// A lambda's parenthesised parameter list beside an expression body (#453, SK-DIV-0050): the list stays
// whole while the line through its `=>` fits — the `=>` as far out as column 120 — and the arrow takes
// the break instead, as a sole argument, among other arguments, after `_f =`, with a `static` modifier
// and under an expression-bodied member. A block body and a body that fits beside the arrow are the
// controls.
class C {
    void M() {
        C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondName) => firstParameterName.Length);
        C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondNameXXXXXXXXXXXXXX) => first.Length);
        C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondNameXXXXXXXXXXXXXXXXXX) => first);
        C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondNameXXXXXXXXXXXXXXXXXXXX) => f);
        C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondNameXXXXXXXXXXXXXXXXXXXXXXXX) => f);
        C2(a, (SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondName) => firstParameterName.Length);
        _f = (SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondNameXXXXXX) => firstParameterName.Length;
        C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondName) => { return firstParameterName.Length; });
        C2(static (SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondName) => firstParameterName.Length);
    }

    int P(int x) => Run((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondName) => firstParameterName.Length);
}
