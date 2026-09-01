using System;

static class LocalCaptureFixture {
    public static Func<int> Build() {
        var seed = 2;
        return () => seed;
    }
}
