// The attribute and the syntax disagree, and which one the author meant is not a guess to make.
using JetBrains.Annotations;

public sealed class Person {
    [NotNull]
    public string? Name { get; set; }
}

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
