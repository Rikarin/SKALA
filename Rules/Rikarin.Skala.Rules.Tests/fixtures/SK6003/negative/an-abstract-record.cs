public abstract record Shape(int Sides) {
    public Shape(int sides, string name) : this(sides) {
        Name = name;
    }

    public string Name { get; } = "shape";
}
