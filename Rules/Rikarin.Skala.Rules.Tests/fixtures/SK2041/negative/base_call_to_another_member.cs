sealed class Note {
    public string Text { get; init; } = "";

    public override bool Equals(object? other) => other is Note note && note.Text == Text;

    public override int GetHashCode() => base.ToString()!.Length;
}
