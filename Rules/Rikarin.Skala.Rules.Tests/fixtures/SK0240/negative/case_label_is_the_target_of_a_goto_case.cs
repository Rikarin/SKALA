class C {
    // ⚠ `case 2:` shares its section with `default:` and is still the target of a jump. Deleting the
    // label turns a redundancy into CS0159, so any `goto case` in the switch withdraws every label
    // finding in it — matching the jump's expression to the label's would mean comparing two
    // constant expressions without a semantic model.
    public static void Run(int value) {
        switch (value) {
            case 1:
                Use(value);
                goto case 2;

            case 2:
            default:
                break;
        }
    }

    static void Use(int value) { }
}
