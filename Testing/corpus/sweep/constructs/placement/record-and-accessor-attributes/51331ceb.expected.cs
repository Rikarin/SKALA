// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
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
