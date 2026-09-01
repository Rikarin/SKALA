using System;

sealed class PropertyCaptureFixture {
    int Factor { get; } = 2;

    public Func<int> Read() => () => Factor;
}
