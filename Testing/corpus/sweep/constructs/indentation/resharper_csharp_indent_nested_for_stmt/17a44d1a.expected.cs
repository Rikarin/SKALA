// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class C {
    void M() {
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++) {
                M();
            }
    }
}
