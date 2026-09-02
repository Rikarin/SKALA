class C {
    // ⚠ `case 2:` names a value that reaches these statements anyway, because `default:` is on the
    // same section. The section does real work, so only the label is deleted.
    public static void Run(int value) {
        switch (value) {
            case 1:
                Use(value);
                break;

            case 2:
            default:
                Reset();
                break;
        }
    }

    static void Use(int value) { }

    static void Reset() { }
}
