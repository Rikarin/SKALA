// The shapes a switch *statement* over an enum may take without omitting anything the rule can
// see: every value listed, a `default`, a pattern the rule cannot enumerate, an alias pair that
// is one value, and a [Flags] enum whose domain is not its declared members.
//
// ⚠ These were switch *expressions* until #280. As expressions they proved nothing after SK2009
// stood down on the form — every one of them would have been cut by the registration rather than
// by the logic they were written to exercise, and a green run could not tell the two apart.

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
    public int Exhaustive(Color color) {
        switch (color) {
            case Color.Red:
                return 1;
            case Color.Green:
                return 2;
            case Color.Blue:
                return 3;
        }

        return 0;
    }

    public int CatchAll(Color color) {
        switch (color) {
            case Color.Red:
                return 1;
            default:
                return 0;
        }
    }

    public int ComplexPattern(Color color) {
        switch (color) {
            case not Color.Red:
                return 1;
        }

        return 0;
    }

    public int Aliases(Alias value) {
        switch (value) {
            case Alias.First:
                return 1;
            case Alias.Second:
                return 2;
        }

        return 0;
    }

    public int FlagsAreOpen(Options options) {
        switch (options) {
            case Options.None:
                return 0;
        }

        return 1;
    }
}
