// `[ItemCanBeNull]` is about the elements, which `?` on the collection type does not say.
using System.Collections.Generic;
using JetBrains.Annotations;

public sealed class Bag {
    [ItemCanBeNull]
    public List<string> Items { get; set; } = new List<string>();
}

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
