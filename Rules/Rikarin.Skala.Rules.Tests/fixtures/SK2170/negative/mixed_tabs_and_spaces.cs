// ⚠ Indentation is compared as a string prefix, never as a column count. One tab and one space are
// the same column and different indentation, so a line whose whitespace is not a prefix of the line
// it is being compared against is declined rather than guessed at. Both directions are here: in
// `HeaderAgainstBody` the body is tab-indented under a space-indented header, and in
// `BodyAgainstNext` the following statement is tab-indented under a space-indented body.
class C {
    void HeaderAgainstBody(bool stale) {
        if (stale)
			Reload();
			Publish();
    }

    void BodyAgainstNext(bool stale) {
        if (stale)
            Reload();
			Publish();
    }

    static void Reload() { }

    static void Publish() { }
}
