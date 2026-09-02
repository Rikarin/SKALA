// The control statement does not begin its own line, so its "indentation" belongs to the statement
// before it and says nothing.
class C {
    void M(bool stale, bool loud) {
        if (loud) Announce(); if (stale)
            Reload();
            Publish();
    }

    static void Announce() { }

    static void Reload() { }

    static void Publish() { }
}
