using System;

static class OuterLocalFunctionFixture {
    public static Func<int> Build(int seed) {
        int Read() => seed;

        return () => Read();
    }
}
