// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class InvocationParens {
    void M() {
        OneArgumentBrokenAtTheParen(
            first
        );
        TwoArgumentsBrokenBetweenThem(
            first,
            second
        );
        TwoArgumentsBrokenAtTheParenAndBetweenThem(
            first,
            second
        );
        NothingBroken(first, second);
        new Constructed(
            first,
            second
        );
    }
}
