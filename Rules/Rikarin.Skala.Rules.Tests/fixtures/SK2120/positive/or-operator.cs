// `Green | Blue` is `1 | 2`, which is `3`, and no member of Color declares 3.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public Color Combine() => Color.Green | Color.Blue;
}
