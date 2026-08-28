// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
