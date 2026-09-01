// A `file` type is visible only inside this file, so the global namespace costs nobody anything.
file sealed class Helper {
    public static int Twice(int value) => value * 2;
}
