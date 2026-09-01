public static class Loader {
    static int Read(string path, bool cache = true) => cache ? path.Length : 0;

    public static int Load(string path) => Read(path, false);
}
