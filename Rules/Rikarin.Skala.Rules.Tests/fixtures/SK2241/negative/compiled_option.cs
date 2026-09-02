using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class CompiledOption {
    // `RegexOptions.Compiled` is stripped before the probe: it emits IL and cannot change whether a
    // pattern parses.
    public static readonly Regex Matcher = new(@"^\w+$", RegexOptions.Compiled);
}
