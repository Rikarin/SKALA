class Base {
    public int Id { get; init; }

    public override bool Equals(object? other) => other is Base other2 && other2.Id == Id;

    public override int GetHashCode() => Id;
}

sealed class Derived : Base {
    public int Extra { get; init; }

    public override bool Equals(object? other) => base.Equals(other) && other is Derived d && d.Extra == Extra;

    public override int GetHashCode() => (base.GetHashCode() * 31) + Extra;
}
