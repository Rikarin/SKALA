// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    int M(int a) {
        var x = 1;
        var y = 2;
        return x + y;
    }

    void N(int a) {
        for (var i = 0; i < a; i++) {
            a++;
            continue;
        }
    }
}
