// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    int [] a;
    int [,] b;
    int [] [] c;

    int [] M() => new int[4];
    int [,] N() => new int[2, 2];
    int [] O() => new int [] { 1 };

    void P(int [] x) {
        int [] y = x;
    }
}
