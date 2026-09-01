class C {
    public static void Run(int[] values) {
        foreach (var value in values) {
            switch (value) {
                case 1:
                    Use(value);
                    continue;

                default:
                    Use(-value);
                    break;
            }
        }
    }

    static void Use(int value) { }
}
