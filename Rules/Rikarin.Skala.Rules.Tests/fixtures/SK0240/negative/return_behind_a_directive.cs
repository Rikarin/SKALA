class C {
    public static void Run(int value) {
        Use(value);
#if DEBUG
        return;
#endif
    }

    static void Use(int value) { }
}
