using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }

    public int Order { get; set; }
}

// Same positional argument, different named one. Not a repetition.
[Tag("audit", Order = 1)]
[Tag("audit", Order = 2)]
sealed class Ledger { }
