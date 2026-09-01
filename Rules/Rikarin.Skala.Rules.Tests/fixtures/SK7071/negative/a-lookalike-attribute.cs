// ⚠ The rule binds the framework type. A rule that matched the spelling would report the class
// below, which has nothing to do with coverage instrumentation.
namespace Vendor;

public sealed class ExcludeFromCodeCoverageAttribute : System.Attribute { }

[ExcludeFromCodeCoverage]
public sealed class Shim { }
