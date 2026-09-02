using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class StaticIsMatch {
    // `pattern` is the *second* parameter here, which is why the argument is resolved by name.
    public static bool Matches(string input) => Regex.IsMatch(input, "(");
}
