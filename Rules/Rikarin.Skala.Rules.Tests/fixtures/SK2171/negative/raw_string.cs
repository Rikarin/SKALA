// A raw string literal has no escapes either, and the token kind says so without a character of the
// text having to be trusted.
class C {
    void M() {
        var single = """\x41B""";
        var multi = """
                    \x41B
                    """;
        Use(single, multi);
    }

    static void Use(string a, string b) { }
}
