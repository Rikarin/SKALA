// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
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
