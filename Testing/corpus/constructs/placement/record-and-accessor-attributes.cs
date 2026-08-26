record Positional([property: First] int X, int Y);

class Accessors {
    int Property {
        [First] get => 1;
        [First] set { }
    }

    int Separated {
        [First]
        get => 2;
    }
}
