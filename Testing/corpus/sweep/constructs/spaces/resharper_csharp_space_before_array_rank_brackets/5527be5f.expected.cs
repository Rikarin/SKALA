// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class C {
    int[] a;
    int[,] b;
    int[][] c;

    int[] M() => new int[4];
    int[,] N() => new int[2, 2];
    int[] O() => new int[] { 1 };

    void P(int[] x) {
        int[] y = x;
    }
}
