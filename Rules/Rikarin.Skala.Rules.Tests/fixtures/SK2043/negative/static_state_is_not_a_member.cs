sealed class Global {
    static int shared = 3;

    public int Id { get; init; }

    public override bool Equals(object? other) => other is Global global && global.Id == Id;

    public override int GetHashCode() => Id + shared;
}
