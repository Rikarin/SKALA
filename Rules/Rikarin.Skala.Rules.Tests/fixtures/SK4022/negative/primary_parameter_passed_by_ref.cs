struct Slot(int value) {
    public void Reset() => Clear(ref value);

    public int Value => value;

    static void Clear(ref int target) => target = 0;
}
