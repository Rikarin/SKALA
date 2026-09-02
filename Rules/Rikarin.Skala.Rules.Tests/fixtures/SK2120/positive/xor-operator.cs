// `^` is a bitwise combination like the other two.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public Color Toggle(Color left, Color right) => left ^ right;
}
