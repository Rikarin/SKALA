using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class OptionsNotConstant {
    // ⚠ The pattern is invalid under `None` and valid under `IgnorePatternWhitespace`, and the options
    // are a parameter — so which one this is cannot be known here. Asking `Regex` the wrong question
    // would produce exactly the false positive the bar forbids, so the call is declined.
    public static Regex Build(RegexOptions options) => new(@"\d+ # (unclosed comment", options);
}
