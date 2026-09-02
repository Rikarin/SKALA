public sealed class Box {
    private string Name { get; }

    public Box(string name) {
        Name = name;
    }

    public string Describe() => Name;
}
