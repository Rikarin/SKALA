using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class ConcatenatedNonConstant {
    public static Regex Build(string suffix) => new("(" + suffix);
}
