// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
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
