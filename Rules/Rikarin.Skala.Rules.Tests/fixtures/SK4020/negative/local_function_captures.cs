static class LocalFunctionCaptureFixture {
    public static int Build(int seed) {
        int Read() => seed;

        return Read();
    }
}
