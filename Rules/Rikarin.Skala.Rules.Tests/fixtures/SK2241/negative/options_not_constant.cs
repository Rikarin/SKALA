using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class OptionsNotConstant {
    // ⚠ The options decide what parses, so a call whose options cannot be folded declines rather than
    // asking `Regex` a question the program does not ask it.
    public static Regex Build(RegexOptions options) => new(@"\d+ # trailing", options);
}
