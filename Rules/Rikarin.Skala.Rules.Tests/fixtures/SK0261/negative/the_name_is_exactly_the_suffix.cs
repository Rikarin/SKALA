// ⚠ Shortening this leaves an empty name. The length guard is what stops it.
namespace N;

sealed class Attribute : System.Attribute { }

[Attribute]
class C { }
