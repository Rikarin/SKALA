// A verbatim literal has no escape sequences at all — that is what it is for — so there is
// nothing to make explicit and the finding would be one nobody could act on.
// contains: U+200B
namespace Fixtures;

sealed class Keys {
    public const string Tenant = @"tenant​id";
}
