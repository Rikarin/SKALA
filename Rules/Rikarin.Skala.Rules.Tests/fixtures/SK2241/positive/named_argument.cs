using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class NamedArgument {
    public static readonly Regex Matcher = new(pattern: "a{2,1}");
}
