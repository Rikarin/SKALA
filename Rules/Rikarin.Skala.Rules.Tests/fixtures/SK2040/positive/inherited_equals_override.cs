class Base {
    public int Id { get; init; }

    public override bool Equals(object? other) => other is Base other2 && other2.Id == Id;

    public override int GetHashCode() => Id;
}

sealed class Derived : Base { }

class C {
    bool Same(Derived left, Derived right) => left == right;
}
