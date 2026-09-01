sealed class RecursionFixture {
    public int Use(int value) => Factorial(value);

    int Factorial(int value) => value <= 1 ? 1 : value * Factorial(value - 1);
}
