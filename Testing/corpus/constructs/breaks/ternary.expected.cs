// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class Ternary {
    void M() {
        var beforeTheSigns = condition
            ? whenTrue
            : whenFalse;

        var afterTheSigns = condition ? whenTrue : whenFalse;

        var joined = condition ? whenTrue : whenFalse;
    }
}
