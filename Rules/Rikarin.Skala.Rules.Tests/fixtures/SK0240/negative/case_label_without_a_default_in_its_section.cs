class C {
    // ⚠ Two labels on one section and no `default:` among them. Neither selects nothing: without
    // `case 2:` the value 2 falls off the end of the switch instead of running `Use`.
    public static void Run(int value) {
        switch (value) {
            case 1:
            case 2:
                Use(value);
                break;

            default:
                Reset();
                break;
        }
    }

    static void Use(int value) { }

    static void Reset() { }
}
