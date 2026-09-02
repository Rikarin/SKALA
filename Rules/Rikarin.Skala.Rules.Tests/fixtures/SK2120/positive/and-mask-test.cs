// A "does it contain" test written against an enum whose members are not bits. `Red` is 0, so the
// mask is zero and the answer is `false` for every input.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public bool Contains(Color color) => (color & Color.Red) != 0;
}
