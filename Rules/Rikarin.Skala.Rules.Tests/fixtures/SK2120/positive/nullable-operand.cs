// The operand type is read through `Nullable<T>`: a lifted `|` combines the same members.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public Color? Combine(Color? left, Color? right) => left | right;
}
