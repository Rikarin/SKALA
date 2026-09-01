using System;

enum Color {
    Red,
    Green,
    Blue
}

enum Alias {
    First = 0,
    AlsoFirst = 0,
    Second = 1
}

[Flags]
enum Options {
    None = 0,
    Read = 1,
    Write = 2
}

sealed class Palette {
    public int Exhaustive(Color color) =>
        color switch {
            Color.Red => 1,
            Color.Green => 2,
            Color.Blue => 3
        };

    public int CatchAll(Color color) =>
        color switch {
            Color.Red => 1,
            _ => 0
        };

    public int ComplexPattern(Color color) =>
        color switch {
            not Color.Red => 1
        };

    public int Aliases(Alias value) =>
        value switch {
            Alias.First => 1,
            Alias.Second => 2
        };

    public int FlagsAreOpen(Options options) =>
        options switch {
            Options.None => 0,
            _ => 1
        };
}
