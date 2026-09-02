using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class RuntimePattern {
    // Not a constant, so whether it parses is not decided here.
    public static Regex Build(string pattern) => new(pattern);
}
