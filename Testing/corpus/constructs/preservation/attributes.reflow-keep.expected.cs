// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
class Attributes {
    [First] [Second] void Joined() { }

    [First]
    [Second]
    void Separated() { }

    [First] int Field;

    [First]
    int Property { get; set; }
}
