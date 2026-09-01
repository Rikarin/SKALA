sealed class Label {
    public string Text { get; init; } = "";

    public bool Equals(string? other) => other == Text;
}
