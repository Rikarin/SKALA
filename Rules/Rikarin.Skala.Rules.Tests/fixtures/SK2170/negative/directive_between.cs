// Under an `#if` the two statements are not necessarily both in the program, and the indentation of
// a conditionally compiled region is a convention rather than a claim.
class C {
    void M(bool stale) {
        if (stale)
            Reload();
#if TRACE
            Publish();
#endif
    }

    static void Reload() { }

    static void Publish() { }
}
