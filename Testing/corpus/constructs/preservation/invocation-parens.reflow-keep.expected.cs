// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class InvocationParens {
    void M() {
        OneArgumentBrokenAtTheParen(first);
        TwoArgumentsBrokenBetweenThem(first, second);
        TwoArgumentsBrokenAtTheParenAndBetweenThem(first, second);
        NothingBroken(first, second);
        new Constructed(first, second);
    }
}
