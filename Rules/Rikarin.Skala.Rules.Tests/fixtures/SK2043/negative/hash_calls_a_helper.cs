sealed class Blob {
    public string Name { get; set; } = "";

    public override bool Equals(object? other) => other is Blob blob && blob.Name == Name;

    public override int GetHashCode() => Mix(3);

    static int Mix(int value) => value * 31;
}
