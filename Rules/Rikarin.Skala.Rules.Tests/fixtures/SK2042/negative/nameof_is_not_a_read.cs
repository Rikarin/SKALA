using System;

sealed class Doc {
    public int Id { get; init; }

    public string Title { get; init; } = "";

    public override bool Equals(object? other) => other is Doc doc && doc.Id == Id;

    public override int GetHashCode() => HashCode.Combine(Id, nameof(Title).Length);
}
