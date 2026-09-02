// `flag` is not a `bool`, so `flag == true` is a user-defined call and deleting it changes the type.
struct Flag {
    public static bool operator ==(Flag left, bool right) => true;

    public static bool operator !=(Flag left, bool right) => false;

    public override bool Equals(object? other) => false;

    public override int GetHashCode() => 0;
}

class C {
    public static bool Run(Flag flag) => flag == true;
}
