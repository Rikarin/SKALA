using System;

static class NestedCaptureFixture {
    public static Func<Func<int>> Build(int seed) => () => () => seed;
}
