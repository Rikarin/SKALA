// A captured primary-constructor parameter that is only read is as readonly as a readonly field:
// `readonly struct Money` compiles.
struct Money(decimal amount) {
    public decimal Amount => amount;

    public Money Doubled() => new(amount * 2);
}
