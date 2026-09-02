// ⚠ Found by the fixture that was written to be a positive. The `#nullable enable` sits in the
// attribute list's *leading trivia*, so the span the fix deletes contains a preprocessor directive
// — removing it would change the nullable context of everything below. The guard reads the list's
// descendant trivia, which is where a directive on the line above ends up.
#nullable disable
using JetBrains.Annotations;

public sealed class Adjacent {
#nullable enable
    [CanBeNull]
    public string Name { get; set; }
#nullable disable
}

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
