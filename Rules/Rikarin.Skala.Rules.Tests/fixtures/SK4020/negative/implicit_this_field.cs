using System;

sealed class FieldCaptureFixture {
    readonly int factor = 2;

    public Func<int> Read() => () => factor;
}
