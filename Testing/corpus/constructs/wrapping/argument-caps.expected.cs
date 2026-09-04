// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class ArgumentCaps {
    void Call() {
        Method(1, 2, 3, 4, 5);
        Other(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
    }

    void Method(int a, int b, int c, int d, int e) { }

    void Other(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l) { }
}

record Positional(int Alpha, int Beta, int Gamma);
