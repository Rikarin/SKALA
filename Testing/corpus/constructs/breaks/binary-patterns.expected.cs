// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class BinaryPatterns {
    bool M(object o) =>
        o is int
            or string
            or bool;

    bool N(object o) => o is int or string or bool;
}
