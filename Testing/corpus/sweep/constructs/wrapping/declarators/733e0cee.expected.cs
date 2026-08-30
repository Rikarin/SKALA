// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class Declarators {
    void Fits() {
        int a = 1,
            b = 2;
    }

    void DoesNotFit() {
        int alphaVariable = 1,
            betaVariable = 2,
            gammaVariable = 3,
            deltaVariable = 4,
            epsilonVariable = 5,
            zetaVar = 6;
    }
}
