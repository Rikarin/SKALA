// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class InvocationParens {
    void M() {
        OneArgumentBrokenAtTheParen(first);
        TwoArgumentsBrokenBetweenThem(first, second);
        TwoArgumentsBrokenAtTheParenAndBetweenThem(first, second);
        NothingBroken(first, second);
        new Constructed(first, second);
    }
}
