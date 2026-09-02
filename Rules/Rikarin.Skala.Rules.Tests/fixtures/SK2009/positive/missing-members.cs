// A switch statement that lists two of Color's three values and forgets Blue: the switch is
// visibly attempting exhaustiveness, so falling out of it is a gap rather than a design.
//
// ⚠ The switch *expression* half of this fixture was removed with #280. `c switch { Red => 1,
// Green => 2 }` is CS8509's, and SK2009 no longer registers for SwitchExpression at all.

enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public int Statement(Color color) {
        switch (color) {
            case Color.Red:
                return 1;
            case Color.Green:
                return 2;
        }

        return 0;
    }
}
