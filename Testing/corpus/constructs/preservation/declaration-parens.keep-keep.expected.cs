// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
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
