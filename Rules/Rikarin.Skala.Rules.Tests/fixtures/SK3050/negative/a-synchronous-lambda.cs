using System;

public sealed class Panel {
    public void Wire() {
        Action callback = () => throw new InvalidOperationException("synchronous");

        callback();
    }
}
