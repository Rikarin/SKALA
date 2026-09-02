// ⚠ Somebody else's `AttributeUsageAttribute` has whatever defaults it declares. The name matches
// and the type does not, which is why the rule asks the type.
namespace N;

[System.AttributeUsage(System.AttributeTargets.All)]
sealed class AttributeUsageAttribute : System.Attribute {
    public AttributeUsageAttribute(System.AttributeTargets targets) { }

    public bool Inherited { get; set; }

    public bool AllowMultiple { get; set; }
}

[AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
sealed class MarkerAttribute : System.Attribute { }
