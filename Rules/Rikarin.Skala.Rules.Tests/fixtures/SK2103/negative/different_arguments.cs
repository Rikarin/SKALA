using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }
}

// The whole point of AllowMultiple: two applications that say different things.
[Tag("audit")]
[Tag("billing")]
sealed class Ledger { }
