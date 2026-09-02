// ⚠ The two operators are deliberately not each other's negation, which the language permits.
// `!(left == right)` is `false` here and `left != right` is `true`.
struct Money {
    public static bool operator ==(Money left, Money right) => true;

    public static bool operator !=(Money left, Money right) => true;

    public override bool Equals(object? other) => false;

    public override int GetHashCode() => 0;
}

class C {
    public static bool Run(Money left, Money right) => !(left == right);
}
