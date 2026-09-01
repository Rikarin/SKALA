public static class Computed {
    static int Read(string path, int limit = 0) => path.Length + limit;

    public static int Load(string path, int limit) => Read(path, limit);
}
