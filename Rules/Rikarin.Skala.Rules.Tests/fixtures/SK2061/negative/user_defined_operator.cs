// A user-defined operator is a method: it is entitled to answer anything, including something
// useful, for two equal operands, and the rule reasons about none of that.
struct Bits {
    public int Value;

    public static Bits operator &(Bits left, Bits right) => new() { Value = left.Value & right.Value };

    public static Bits operator -(Bits left, Bits right) => new() { Value = left.Value - right.Value };
}

class C {
    Bits M(Bits b) => b & b;

    Bits N(Bits b) => b - b;
}
