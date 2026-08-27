// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
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
