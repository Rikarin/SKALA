using System;

sealed class ThisCaptureFixture {
    public Func<object> Self() => () => this;
}
