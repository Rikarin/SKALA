public sealed class Work {
    public int Run(int value) {
        switch (value) {
            case 0:
                goto default;
            case 1:
                goto default;
            default:
                return value;
        }
    }
}
