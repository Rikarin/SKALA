using System;

public sealed class Builder {
    public static Exception Build() {
        var failure = new InvalidOperationException("not started");
        return failure;
    }
}
