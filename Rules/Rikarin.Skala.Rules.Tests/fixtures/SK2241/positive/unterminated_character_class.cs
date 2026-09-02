using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class UnterminatedCharacterClass {
    public static readonly Regex Matcher = new("[a-");
}
