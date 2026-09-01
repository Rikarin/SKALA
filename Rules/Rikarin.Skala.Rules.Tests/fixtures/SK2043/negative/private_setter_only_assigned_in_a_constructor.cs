sealed class Locked {
    public Locked(int id) => Id = id;

    public int Id { get; private set; }

    public override bool Equals(object? other) => other is Locked locked && locked.Id == Id;

    public override int GetHashCode() => Id;
}
