class C {
    bool M(bool ready, bool other) {
        var seen = false;
        return ready & (seen = other);
    }
}
