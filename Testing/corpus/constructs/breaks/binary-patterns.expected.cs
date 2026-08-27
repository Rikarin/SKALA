// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
class BinaryPatterns {
    bool M(object o) =>
        o is int
            or string
            or bool;

    bool N(object o) => o is int or string or bool;
}
