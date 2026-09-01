// Indistinguishable from a space, and not one. A split on U+0020 keeps it in the token.
// contains: U+00A0
namespace Fixtures;

sealed class Headers {
    public const string Accept = "text/html, application/json";
}
