// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class Ternary {
    void M() {
        var beforeTheSigns = condition
            ? whenTrue
            : whenFalse;

        var afterTheSigns = condition ? whenTrue : whenFalse;

        var joined = condition ? whenTrue : whenFalse;
    }
}
