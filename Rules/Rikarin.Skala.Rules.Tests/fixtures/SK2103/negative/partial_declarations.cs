using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }
}

// ⚠ Two `partial` declarations of one type are not compared. The repetition is real, but the
// repair is a judgement about which declaration keeps it, and the answer would stop being
// decidable from the file the finding is in — which is what `scope: Semantic` promises the cache.
[Tag("audit")]
partial class Ledger { }

[Tag("audit")]
partial class Ledger { }
