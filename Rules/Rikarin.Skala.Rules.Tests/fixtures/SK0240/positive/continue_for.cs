class C {
    public static void Run(int[] values) {
        for (var i = 0; i < values.Length; i++) {
            Use(values[i]);
            continue;
        }
    }

    static void Use(int value) { }
}
