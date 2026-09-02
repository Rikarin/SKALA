// A type with no user-defined operator at all, and one with only a comparison pair. Neither can
// reach the rule, and the walk over every named type in a compilation has to be cheap on them.
using System;

class Plain {
    public int Value { get; set; }

    public int Add(int other) => Value + other;
}

struct Comparable : IComparable<Comparable> {
    public int Value;

    public int CompareTo(Comparable other) => Value.CompareTo(other.Value);

    public static bool operator <(Comparable a, Comparable b) => a.Value < b.Value;

    public static bool operator >(Comparable a, Comparable b) => a.Value > b.Value;
}
