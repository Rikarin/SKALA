class C {
    int writes;

    public int this[int index] {
        get => index;
        set { writes++; }
    }
}
