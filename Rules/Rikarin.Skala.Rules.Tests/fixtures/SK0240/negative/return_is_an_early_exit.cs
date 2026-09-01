class C {
    public static void Run(int value) {
        if (value < 0) {
            return;
        }

        Use(value);
    }

    static void Use(int value) { }
}
