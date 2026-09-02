// A verbatim literal has no escapes: `@"\x41B"` is six characters. Reporting it would be reporting
// text that does not mean what the rule assumes.
class C {
    void M() {
        var pattern = @"\x41B";
        var interpolated = $@"\x41B{pattern}";
        Use(pattern, interpolated);
    }

    static void Use(string a, string b) { }
}
