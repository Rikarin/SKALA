// A UTF-8 literal is its own SyntaxKind and takes no escapes either.
// contains: U+200B
namespace Fixtures;

sealed class Keys {
    public static System.ReadOnlySpan<byte> Tenant => "tenant​id"u8;
}
