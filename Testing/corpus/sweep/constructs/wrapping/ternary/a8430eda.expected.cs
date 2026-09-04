// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class Ternary {
    string Fits(int a, int b) => a > b ? "left" : "right";

    string DoesNotFit(int a, int b) {
        var t = a > b ? "the first value is larger than the second one" : "the second value is larger than or equal";
        return t;
    }
}
