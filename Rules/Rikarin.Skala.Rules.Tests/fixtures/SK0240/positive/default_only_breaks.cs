class C {
    public static void Run(int value) {
        switch (value) {
            case 1:
                Use(value);
                break;

            default:
                break;
        }
    }

    static void Use(int value) { }
}
