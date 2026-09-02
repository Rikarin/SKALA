using System.Text.RegularExpressions;

// ⚠ Vixen's own test assertions are this shape, twelve times. Sonar's `S6444` reports every one of
// them as a vulnerability; there is no group here to repeat, so this rule does not.
public static class Probe {
    public static bool Mentions(string source) => new Regex(@"UnitScale\s*=\s*4294967296f").IsMatch(source);
}
