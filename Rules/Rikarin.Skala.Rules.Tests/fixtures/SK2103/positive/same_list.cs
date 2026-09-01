using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }
}

// Two applications in one list, where the fix has to take a separator with it.
[Tag("audit"), Tag("audit")]
sealed class Ledger { }
