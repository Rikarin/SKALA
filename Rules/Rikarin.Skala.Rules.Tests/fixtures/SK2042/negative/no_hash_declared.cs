sealed class Plain {
    public int Id { get; init; }

    public string Name { get; init; } = "";

    public override bool Equals(object? other) => other is Plain plain && plain.Id == Id;
}
