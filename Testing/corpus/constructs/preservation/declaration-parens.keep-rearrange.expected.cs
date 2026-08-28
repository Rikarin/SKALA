// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class DeclarationParens {
    void OneParameterBrokenAtTheParen(int first) { }

    void TwoParametersBrokenBetweenThem(
        int first,
        int second
    ) { }

    void TwoParametersFullyBroken(
        int first,
        int second
    ) { }

    void NothingBroken(int first, int second) { }
}
