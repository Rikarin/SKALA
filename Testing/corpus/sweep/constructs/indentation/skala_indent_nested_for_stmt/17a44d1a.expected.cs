// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class C {
    void M() {
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++) {
                M();
            }
    }
}
