using System;

public sealed class ExceptionHandler {
    public void Handle(Exception error) => Last = error;

    public Exception? Last { get; private set; }
}

public sealed class ExceptionFilter {
    public bool Matches(Exception error) => error is InvalidOperationException;
}

public sealed class ExceptionPolicy {
    public int Retries { get; init; }
}

public static class ExceptionExtensions {
    public static string Describe(this Exception error) => error.Message;
}
