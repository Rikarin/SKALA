// ⚠ The reason `[MaybeNull]` exists: an unconstrained `T` is not a reference type, and `T?`
// there says something different from what the attribute says.
using JetBrains.Annotations;

public sealed class Store {
    [CanBeNull]
    public T Get<T>() => default!;
}

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
