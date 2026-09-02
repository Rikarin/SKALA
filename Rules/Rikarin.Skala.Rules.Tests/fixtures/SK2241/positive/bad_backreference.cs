using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class BadBackreference {
    public static readonly Regex Matcher = new(@"\1abc");
}
