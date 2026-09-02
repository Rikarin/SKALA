using System.Text.RegularExpressions;

// ⚠ `\(` and `\)` are literal parentheses. The trailing `+` repeats the literal `)`, not a group —
// a scanner that ignored escapes would see `(a+)+` here.
public static class Tokens {
    public static bool Call(string input) => new Regex(@"\(a+\)+").IsMatch(input);
}
