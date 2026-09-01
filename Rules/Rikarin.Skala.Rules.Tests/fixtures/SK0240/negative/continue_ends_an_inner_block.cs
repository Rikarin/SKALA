class C {
    public static void Run(int[] values) {
        for (var i = 0; i < values.Length; i++) {
            if (values[i] < 0) {
                Use(values[i]);
                continue;
            }

            Use(-values[i]);
        }
    }

    static void Use(int value) { }
}
