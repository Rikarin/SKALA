public static class Statistics {
    static readonly object Gate = new();

    static int samples;

    public static void Record() {
        lock (Gate) {
            samples++;
        }
    }

    public static int Read() {
        lock (Gate) {
            return samples;
        }
    }

    public static void Clear() {
        samples = 0;
    }
}
