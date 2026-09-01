// A user-defined operator is a method: it is entitled to answer something useful for two equal
// operands, and the rule reasons about none of that.
struct Version {
    public int Major;

    public static bool operator ==(Version left, Version right) => left.Major == right.Major;

    public static bool operator !=(Version left, Version right) => !(left == right);

    public override bool Equals(object? other) => other is Version v && this == v;

    public override int GetHashCode() => Major;
}

class C {
    bool M(Version v) => v == v;
}
