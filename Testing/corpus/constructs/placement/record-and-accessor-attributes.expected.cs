// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
record Positional(
    [property: First]
    int X,
    int Y);

class Accessors {
    int Property {
        [First]
        get => 1;
        [First]
        set { }
    }

    int Separated {
        [First]
        get => 2;
    }
}
