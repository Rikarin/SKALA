// `~Green` is `~1`, which is `-2`. Nothing in Color is negative.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public Color Invert(Color color) => ~color;
}
