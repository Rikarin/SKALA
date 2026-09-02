// Cast to the underlying type and the operands are no longer enum members. Whatever the author is
// doing with the bits, they said so.
enum Color {
    Red,
    Green,
    Blue
}

sealed class Palette {
    public int Combine(Color left, Color right) => (int)left | (int)right;

    public int Mask(Color color) => (int)color & 0xFF;

    public int Invert(Color color) => ~(int)color;
}
