// The type says, in its own source, that overflow on it is meant to trap — and then says it for
// `+` and not for `-`. Inside one `checked` block the two behave oppositely.
struct Money {
    public long Cents;

    public static Money operator +(Money a, Money b) => new Money { Cents = a.Cents + b.Cents };

    public static Money operator checked +(Money a, Money b) =>
        checked(new Money { Cents = a.Cents + b.Cents });

    public static Money operator -(Money a, Money b) => new Money { Cents = a.Cents - b.Cents };

    public static Money operator *(Money a, long b) => new Money { Cents = a.Cents * b };
}
