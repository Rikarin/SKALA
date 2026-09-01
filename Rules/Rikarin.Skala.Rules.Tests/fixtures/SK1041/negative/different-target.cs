public sealed class Sums {
    int total;
    int subtotal;

    // This is the typo the rule exists to make impossible, not an instance of it.
    public void Recompute() {
        total = subtotal + 1;
    }

    public int Value => total;
}
