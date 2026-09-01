using System;

sealed class MethodCaptureFixture {
    public Func<int> Read() => () => Seed();

    int Seed() => 2 + GetHashCode();
}
