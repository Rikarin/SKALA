using System;

static class ParameterCaptureFixture {
    public static Func<int> Build(int seed) => () => seed;
}
