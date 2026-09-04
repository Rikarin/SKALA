// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
record Positional([property: First] int X, int Y);

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
