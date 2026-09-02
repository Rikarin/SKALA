public sealed class Dispatching {
    static int Slow(int key) => key + 1;

    public static int Route(int key) {
        switch (key) {
            case 0:
                int result;
                result = Slow(key);
                return result + key;

            default:
                return 0;
        }
    }
}
