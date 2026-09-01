class C {
    int stored;

    public int Value {
        get => stored;
        set {
            stored = value;
            return;
        }
    }
}
