// The compound form hides the same combination behind an assignment.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public Color Accumulate(Color color) {
        color |= Color.Blue;
        return color;
    }
}
