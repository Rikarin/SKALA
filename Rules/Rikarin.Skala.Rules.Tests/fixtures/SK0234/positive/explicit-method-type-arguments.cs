public static class Echoing {
    static T Echo<T>(T value) => value;

    public static int Same(int value) => Echo<int>(value);
}
