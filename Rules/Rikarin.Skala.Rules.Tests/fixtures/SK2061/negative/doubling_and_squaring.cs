// `+`, `*`, `<<` and `>>` are not examined: doubling, squaring and shifting by oneself are
// ordinary arithmetic.
class C {
    int Twice(int x) => x + x;

    int Square(int x) => x * x;

    int Shift(int x) => x << x;
}
