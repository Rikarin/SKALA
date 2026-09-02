// ⚠ `\\x41B` is a backslash followed by `x41B`, not an escape. Consuming both characters of every
// `\\` is the whole of the difference between this rule and a search for the two-character string
// `\x`.
class C {
    void M() {
        var pattern = "\\x41B";
        var doubled = "a\\\\x41B";
        Use(pattern, doubled);
    }

    static void Use(string a, string b) { }
}
