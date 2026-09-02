using JetBrains.Annotations;

public sealed class Record {
    [CanBeNull]
    public string Note;
}

namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class CanBeNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class NotNullAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    sealed class ItemCanBeNullAttribute : System.Attribute { }
}
