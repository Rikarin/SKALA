// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
record Positional(
    [property: First]
    int X,
    int Y);

class Accessors {
    int Property {
        [First] get => 1;
        [First] set { }
    }

    int Separated {
        [First] get => 2;
    }
}
