// Everything a consecutively numbered enum is actually for. The rule is about one operator family
// and must be silent on the rest of the type's life.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public int Rank(Color color) =>
        color switch {
            Color.Red => 0,
            Color.Green => 1,
            Color.Blue => 2,
            _ => -1
        };

    public bool Warmer(Color left, Color right) => left < right;

    public string Name(Color color) => color.ToString();

    public Color Next(Color color) => color + 1;
}
