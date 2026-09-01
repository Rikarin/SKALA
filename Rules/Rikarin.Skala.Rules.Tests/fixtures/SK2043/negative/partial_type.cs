partial class Split {
    public string Name { get; set; } = "";
}

partial class Split {
    public override bool Equals(object? other) => other is Split split && split.Name == Name;

    public override int GetHashCode() => Name.GetHashCode();
}
