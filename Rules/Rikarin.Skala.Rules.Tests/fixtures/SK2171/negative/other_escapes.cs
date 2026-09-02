// Every escape that is not `\x` has a fixed meaning and a fixed length.
class C {
    void M() {
        var text = "line\n\ttabbed\"quoted\"\r\0\a\b\f\v";
        Use(text);
    }

    static void Use(string value) { }
}
