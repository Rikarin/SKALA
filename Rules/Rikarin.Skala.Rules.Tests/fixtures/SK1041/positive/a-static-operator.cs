// A type with only a static `operator +` binds the same method in both forms, so the rewrite holds.
public sealed class Money {
    public readonly int Cents;

    public Money(int cents) => Cents = cents;

    public static Money operator +(Money a, int b) => new Money(a.Cents + b);
}

public static class Probe {
    public static int Run() {
        var wallet = new Money(5);
        var alias = wallet;
        wallet = wallet + 2;
        return wallet.Cents * 100 + alias.Cents;
    }
}
