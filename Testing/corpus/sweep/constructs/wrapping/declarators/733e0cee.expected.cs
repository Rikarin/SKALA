// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
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
