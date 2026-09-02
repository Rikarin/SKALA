// Two things at once: an explicit conversion with a checked form and a second one without, and a
// second `+` overload whose parameter types differ from the one that has a checked counterpart.
struct Counter {
    public long Value;

    public static Counter operator +(Counter a, Counter b) => new Counter { Value = a.Value + b.Value };

    public static Counter operator checked +(Counter a, Counter b) =>
        checked(new Counter { Value = a.Value + b.Value });

    // A different overload of `+`. The checked form above is not its counterpart.
    public static Counter operator +(Counter a, long b) => new Counter { Value = a.Value + b };

    public static explicit operator int(Counter a) => (int)a.Value;

    public static explicit operator short(Counter a) => (short)a.Value;

    public static explicit operator checked short(Counter a) => checked((short)a.Value);
}
