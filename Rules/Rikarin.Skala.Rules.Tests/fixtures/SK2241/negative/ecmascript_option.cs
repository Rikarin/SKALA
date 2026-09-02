using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class EcmascriptOption {
    // ⚠ The second options discriminator, measured rather than assumed: `\1` with no group 1 is
    // "Reference to undefined group number 1" under `None` and is accepted under `ECMAScript`, which
    // treats it as an octal escape. `bad_backreference.cs` is this same pattern without the option and
    // is a positive.
    public static readonly Regex Matcher = new(@"\1abc", RegexOptions.ECMAScript);
}
