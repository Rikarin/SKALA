// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class BinaryOperators {
    void M() {
        var beforeTheSign = first
            + second
            + third;

        var afterTheSign = first + second + third;

        var mixed = first && second
            || third;

        var coalescing = first
            ?? second;
    }
}
