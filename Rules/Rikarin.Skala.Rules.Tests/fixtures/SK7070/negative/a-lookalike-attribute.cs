// ⚠ The rule binds `System.ObsoleteAttribute`, so somebody else's `Obsolete` is not its business.
// A rule that matched the spelling would report the method below.
namespace Vendor;

public sealed class ObsoleteAttribute : System.Attribute { }

public sealed class Store {
    [Obsolete]
    public void Save() { }
}
