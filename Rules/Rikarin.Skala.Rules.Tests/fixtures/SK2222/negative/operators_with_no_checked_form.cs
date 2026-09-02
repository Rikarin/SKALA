// ⚠ The list the compiler settled. `%`, `&`, `|`, `^`, `<<`, `>>`, `~`, `==`, `!=` and unary `+`
// cannot be declared checked at all — CS9023 — and an implicit conversion cannot either, CS9024.
// A rule that asked for a checked counterpart to any of these would be asking for a declaration
// that does not compile. The type declares a checked `+`, so the rule is live in this file.
struct Bits {
    public long Value;

    public static Bits operator +(Bits a, Bits b) => new Bits { Value = a.Value + b.Value };

    public static Bits operator checked +(Bits a, Bits b) => checked(new Bits { Value = a.Value + b.Value });

    public static Bits operator +(Bits a) => a;

    public static Bits operator %(Bits a, Bits b) => new Bits { Value = a.Value % b.Value };

    public static Bits operator &(Bits a, Bits b) => new Bits { Value = a.Value & b.Value };

    public static Bits operator |(Bits a, Bits b) => new Bits { Value = a.Value | b.Value };

    public static Bits operator ^(Bits a, Bits b) => new Bits { Value = a.Value ^ b.Value };

    public static Bits operator ~(Bits a) => new Bits { Value = ~a.Value };

    public static Bits operator <<(Bits a, int b) => new Bits { Value = a.Value << b };

    public static Bits operator >>(Bits a, int b) => new Bits { Value = a.Value >> b };

    public static bool operator ==(Bits a, Bits b) => a.Value == b.Value;

    public static bool operator !=(Bits a, Bits b) => a.Value != b.Value;

    public static implicit operator long(Bits a) => a.Value;

    public override bool Equals(object obj) => obj is Bits other && other.Value == Value;

    public override int GetHashCode() => Value.GetHashCode();
}
