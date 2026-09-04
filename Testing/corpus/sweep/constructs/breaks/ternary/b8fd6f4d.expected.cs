// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class Ternary {
    void M() {
        var beforeTheSigns = condition
            ? whenTrue
            : whenFalse;

        var afterTheSigns = condition ? whenTrue : whenFalse;

        var joined = condition ? whenTrue : whenFalse;
    }
}
