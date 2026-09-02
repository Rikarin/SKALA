using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class WellFormed {
    public static readonly Regex Matcher = new(@"^\d{3}-\d{4}$");
}
