// ⚠ A real hole rather than a tidy boundary: a bidirectional override in a raw string is
// exactly as dangerous and is not reported, because no escape can be written to repair it.
// contains: U+202E
namespace Fixtures;

sealed class Keys {
    public const string Tenant = """tenant‮id""";
}
