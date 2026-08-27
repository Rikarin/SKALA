// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
class BinaryPatterns {
    bool M(object o) =>
        o is int
            or string
            or bool;

    bool N(object o) => o is int or string or bool;
}
