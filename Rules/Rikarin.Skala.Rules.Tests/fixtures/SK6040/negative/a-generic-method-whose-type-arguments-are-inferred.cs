public static class Inference {
    public static bool Check(int token) => TryUnwrap(token, out string content);

    static bool TryUnwrap<T>(int token, out T value) {
        value = default!;

        return token > 0;
    }
}
