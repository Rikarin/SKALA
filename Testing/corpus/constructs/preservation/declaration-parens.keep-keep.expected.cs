// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class DeclarationParens {
    void OneParameterBrokenAtTheParen(
        int first
    ) { }

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
