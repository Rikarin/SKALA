// ⚠ The guard the whole rule is built around, and the shape almost every arithmetic type in every
// repository has. Nothing here says overflow on `Money` is meant to trap, so there is no
// inconsistency to report — only an opinion nobody asked for.
struct Money {
    public long Cents;

    public static Money operator +(Money a, Money b) => new Money { Cents = a.Cents + b.Cents };

    public static Money operator -(Money a, Money b) => new Money { Cents = a.Cents - b.Cents };

    public static Money operator *(Money a, long b) => new Money { Cents = a.Cents * b };

    public static Money operator /(Money a, long b) => new Money { Cents = a.Cents / b };

    public static explicit operator int(Money a) => (int)a.Cents;
}
