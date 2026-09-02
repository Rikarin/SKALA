// The repair, and the shape the rule must be quiet on: `\u` has a fixed length of four.
class C {
    void M() {
        var cyrillic = "\u041B";
        var tab = '\u0009';
        Use(cyrillic, tab);
    }

    static void Use(string a, char b) { }
}
