// ⚠ The context is read at the declaration, not at the file. The file opens with annotations
// off and turns them back on around this type, so the attribute is expressible here and the rule
// fires — which is the half of the context check a `#nullable disable` fixture cannot prove.
#nullable disable
using JetBrains.Annotations;

#nullable enable
public sealed class Restored {
    [CanBeNull]
    public string Name { get; set; }
}
#nullable disable

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
