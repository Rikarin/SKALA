using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }

    public int Order { get; set; }
}

// Named arguments are compared by name, not by position.
[Tag("audit", Order = 2)]
[Tag("audit", Order = 2)]
sealed class Ledger { }
