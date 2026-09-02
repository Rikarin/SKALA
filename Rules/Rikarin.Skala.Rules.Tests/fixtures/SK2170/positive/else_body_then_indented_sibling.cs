class C {
    void M(bool fast) {
        if (fast)
            Quick();
        else
            Careful();
            Finish();
    }

    static void Quick() { }

    static void Careful() { }

    static void Finish() { }
}
