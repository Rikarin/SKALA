using System;

static class AnonymousMethodCaptureFixture {
    public static Func<int, int> Build(int seed) => delegate(int value) { return value + seed; };
}
