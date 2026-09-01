using System;

static class AnonymousMethodFixture {
    public static Func<int, int> Identity() => delegate(int value) { return value; };
}
