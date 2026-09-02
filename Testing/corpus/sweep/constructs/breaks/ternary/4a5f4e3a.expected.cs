// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class Ternary {
    void M() {
        var beforeTheSigns = condition ? whenTrue : whenFalse;

        var afterTheSigns = condition ?
            whenTrue :
            whenFalse;

        var joined = condition ? whenTrue : whenFalse;
    }
}
