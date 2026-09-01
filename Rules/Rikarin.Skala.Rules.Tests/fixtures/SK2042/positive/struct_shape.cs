using System;

struct Tag {
    public int Id;

    public string Name;

    public override bool Equals(object? other) => other is Tag tag && tag.Id == Id;

    public override int GetHashCode() => HashCode.Combine(Id, Name);
}
