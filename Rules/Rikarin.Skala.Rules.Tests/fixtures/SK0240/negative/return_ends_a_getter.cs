class C {
    int stored;

    public int Value {
        get {
            Use(stored);
            return stored;
        }
    }

    static void Use(int value) { }
}
