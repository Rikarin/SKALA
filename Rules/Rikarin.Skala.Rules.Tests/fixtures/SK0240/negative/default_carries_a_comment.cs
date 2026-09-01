class C {
    public static void Run(int value) {
        switch (value) {
            case 1:
                Use(value);
                break;

            default:
                // Every other opcode is handled by the outer dispatcher.
                break;
        }
    }

    static void Use(int value) { }
}
