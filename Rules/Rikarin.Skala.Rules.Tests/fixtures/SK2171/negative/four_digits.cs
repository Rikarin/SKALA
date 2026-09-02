// Four hex digits is all `\x` can take, so the escape has consumed everything it could and its
// length is not in question.
class C {
    void M() {
        var cyrillic = "\x041B";
        var ascii = "\x0041B";
        Use(cyrillic, ascii);
    }

    static void Use(string a, string b) { }
}
