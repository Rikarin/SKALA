// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class Attributes {
    [First] [Second] void Joined() { }

    [First]
    [Second]
    void Separated() { }

    [First] int Field;

    [First]
    int Property { get; set; }
}
