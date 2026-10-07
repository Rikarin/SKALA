// A captured primary-constructor parameter is storage the struct writes. `readonly struct Budget`
// makes it readonly too, and `total -= amount` becomes CS9114.
struct Budget(decimal total) {
    public void Spend(decimal amount) => total -= amount;

    public decimal Remaining => total;
}
