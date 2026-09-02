using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class NothingToRepeat {
    public static readonly Regex Matcher = new("*abc");
}
