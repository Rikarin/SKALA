using System;

public sealed class Builder {
    public static Exception Build() => new InvalidOperationException("not started");
}
