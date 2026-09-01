using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }

    public int Order { get; set; }
}

[Tag("audit")]
[Tag("audit", Order = 1)]
sealed class Ledger { }
