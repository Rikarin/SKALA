class C {
    void M(bool stale) {
        if (stale) Reload();
        Publish();
    }

    static void Reload() { }

    static void Publish() { }
}
