using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }
}

// ⚠ A `name:` argument makes position and name two different orderings of one list, so the pair
// is declined rather than guessed at.
[Tag(name: "audit")]
[Tag(name: "audit")]
sealed class Ledger { }
