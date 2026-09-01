using System;

partial class Split {
    public int Id { get; init; }

    public string Name { get; init; } = "";
}

partial class Split {
    public override bool Equals(object? other) => other is Split split && split.Id == Id;

    public override int GetHashCode() => HashCode.Combine(Id, Name);
}
