// ⚠ #423: the offset keeps its place relative to everything observable, but a getter there could
// change what the search value reads, and the search value now runs first.
public sealed class Scanner {
    string needle = "b";

    int Start {
        get {
            needle = "c";
            return 1;
        }
    }

    public bool Scan(string text) => text.Substring(Start).IndexOf(needle, System.StringComparison.Ordinal) >= 0;
}

public static class Probe {
    public static bool Run() => new Scanner().Scan("abab");
}
