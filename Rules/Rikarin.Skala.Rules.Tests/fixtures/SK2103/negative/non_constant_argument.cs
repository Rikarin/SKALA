using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagsAttribute : Attribute {
    public TagsAttribute(string[] names) => Names = names;

    public string[] Names { get; }
}

// ⚠ Identity is proved, never assumed. An array creation is not a compile-time constant, so the
// pair is declined even though the two are written identically.
[Tags(new[] { "audit" })]
[Tags(new[] { "audit" })]
sealed class Ledger { }
