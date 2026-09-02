using System.Text.RegularExpressions;

// ⚠ Inside a character class `(`, `*` and `+` are literal characters, not structure. A scanner that
// did not skip classes would read this as a quantified group and report it.
public static class Tokens {
    public static bool Punctuation(string input) => new Regex(@"[(*+]+").IsMatch(input);
}
