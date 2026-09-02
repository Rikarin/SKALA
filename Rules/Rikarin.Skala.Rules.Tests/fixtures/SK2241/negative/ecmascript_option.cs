using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class EcmascriptOption {
    public static readonly Regex Matcher = new(@"^\d+$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
}
