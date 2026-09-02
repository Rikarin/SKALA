class C {
    // ⚠ One finding, not two, and the whole section is what goes. Deleting only `case 2:` would
    // leave `default: break;` — this rule's other switch shape — so the fix's own output would still
    // carry a finding. Every value the section named falls off the end of the switch instead, which
    // is exactly what `break` did.
    public static void Run(int value) {
        switch (value) {
            case 1:
                Use(value);
                break;

            case 2:
            default:
                break;
        }
    }

    static void Use(int value) { }
}
