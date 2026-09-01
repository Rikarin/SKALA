class C {
    public static void Run(int value) {
        switch (value) {
            case 1:
                goto default;

            default:
                break;
        }
    }
}
