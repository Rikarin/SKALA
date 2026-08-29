// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class BinaryPatterns {
    bool M(object o) =>
        o is int
            or string
            or bool;

    bool N(object o) => o is int or string or bool;
}
