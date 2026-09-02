namespace Ours;

// A type of the same name is not the framework's. The rule compares symbols, not identifiers.
public sealed class Regex {
    public Regex(string pattern) => Pattern = pattern;

    public string Pattern { get; }

    public bool IsMatch(string input) => input == Pattern;
}

public static class Validator {
    public static bool Looks(string input) => new Regex(@"^(a+)+$").IsMatch(input);
}
