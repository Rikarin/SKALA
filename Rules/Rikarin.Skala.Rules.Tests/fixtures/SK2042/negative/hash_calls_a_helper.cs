sealed class Blob {
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public override bool Equals(object? other) => other is Blob blob && blob.Id == Id;

    public override int GetHashCode() => Mix(Id) + Name.Length;

    static int Mix(int value) => value * 31;
}
