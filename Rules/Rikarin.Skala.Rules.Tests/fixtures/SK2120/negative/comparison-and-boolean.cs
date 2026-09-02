// `==`, `!=` and the boolean operators are not bitwise combinations of members. `&` and `|` on
// two `bool`s are a different question, and SK2064 is the rule that asks it.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public bool Same(Color left, Color right) => left == right;

    public bool Different(Color left, Color right) => left != right;

    public bool Either(Color color) => color == Color.Red || color == Color.Green;

    public bool Both(bool left, bool right) => left && right;
}
