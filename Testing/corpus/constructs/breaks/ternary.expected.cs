// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
class Ternary {
    void M() {
        var beforeTheSigns = condition
            ? whenTrue
            : whenFalse;

        var afterTheSigns = condition ? whenTrue : whenFalse;

        var joined = condition ? whenTrue : whenFalse;
    }
}
