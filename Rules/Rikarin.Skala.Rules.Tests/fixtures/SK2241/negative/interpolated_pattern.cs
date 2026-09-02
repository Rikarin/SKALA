using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class InterpolatedPattern {
    public static Regex Build(string prefix) => new($"^{prefix}(");
}
