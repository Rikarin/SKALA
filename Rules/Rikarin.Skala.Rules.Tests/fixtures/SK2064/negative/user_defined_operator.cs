// A user-defined `operator &` is a method call. Whatever it means, it is not short-circuitable.
struct Tri {
    public int Value;

    public static Tri operator &(Tri left, Tri right) => new() { Value = left.Value & right.Value };

    public static Tri operator |(Tri left, Tri right) => new() { Value = left.Value | right.Value };
}

class C {
    Tri M(Tri a, Tri b) => a & b;
}
