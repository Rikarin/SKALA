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

    public int Expression(Color color) =>
        color switch {
            Color.Red => 1,
            Color.Green => 2
        };
}
