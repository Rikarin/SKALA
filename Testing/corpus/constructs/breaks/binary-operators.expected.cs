// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
