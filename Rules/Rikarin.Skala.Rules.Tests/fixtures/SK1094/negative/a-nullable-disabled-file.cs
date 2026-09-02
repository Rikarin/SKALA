// ⚠ The fixture the rule exists to be constrained by. With annotations off the attribute is the
// only statement of nullability in the file, and `string?` here is CS8632 and a lost fact.
#nullable disable
using JetBrains.Annotations;

public sealed class Person {
    [CanBeNull]
    public string Name { get; set; }
}

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
