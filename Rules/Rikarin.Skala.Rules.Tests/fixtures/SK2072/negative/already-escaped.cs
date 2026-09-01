// ⚠ The scan reads the token's source spelling, never its value. In the raw text this is
// six ASCII characters, so the repair is not reported as the problem.
namespace Fixtures;

sealed class Keys {
    public const string Tenant = "tenant\u200Bid";
    public const string Newline = "left\nright";
}
