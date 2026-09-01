using System;

public sealed class RetryLimitReachedException : Exception {
    public RetryLimitReachedException(string message) : base(message) { }
}
