// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
class InvocationParens {
    void M() {
        OneArgumentBrokenAtTheParen(first);
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
