// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class BinaryOperators {
    void M() {
        var beforeTheSign = first + second + third;

        var afterTheSign = first +
            second +
            third;

        var mixed = first && second || third;

        var coalescing = first ?? second;
    }
}
