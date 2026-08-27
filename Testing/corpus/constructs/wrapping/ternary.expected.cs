// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
class Ternary {
    string Fits(int a, int b) => a > b ? "left" : "right";

    string DoesNotFit(int a, int b) {
        var t = a > b ? "the first value is larger than the second one" : "the second value is larger than or equal";
        return t;
    }
}
