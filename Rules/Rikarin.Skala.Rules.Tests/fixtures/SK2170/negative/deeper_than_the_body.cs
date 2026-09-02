// ⚠ Alignment, not "at least as deep", and the corpus settled it. The looser test reported four
// times on `unformatted/scramble/`, a slice whose whitespace has been randomised on purpose: there
// the following statement lands 2, 4 or 6 columns past the body, which reads as mangled or as a
// continuation and not as a sibling. Asking for the column a reader would actually see declines all
// four, and this file.
class C {
    void M(int[] data) {
        foreach (var value in data)
            Record(value);
                Flush();
    }

    static void Record(int value) { }

    static void Flush() { }
}
