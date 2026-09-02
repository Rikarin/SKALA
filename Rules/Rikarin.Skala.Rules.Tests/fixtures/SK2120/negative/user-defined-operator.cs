// A `|` that resolves to somebody's own operator is that type's designed API, not a member
// combination. The `OperatorMethod: null` guard is what declines it.
readonly struct Mask {
    readonly int bits;

    public Mask(int bits) => this.bits = bits;

    public static Mask operator |(Mask left, Mask right) => new(left.bits | right.bits);

    public static Mask operator &(Mask left, Mask right) => new(left.bits & right.bits);

    public static Mask operator ~(Mask value) => new(~value.bits);
}

sealed class Builder {
    public Mask Combine(Mask left, Mask right) => left | right;

    public Mask Invert(Mask value) => ~value;
}
