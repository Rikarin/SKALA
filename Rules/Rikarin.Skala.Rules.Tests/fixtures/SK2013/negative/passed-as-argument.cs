using System;

public sealed class Reporter {
    public static void Report(Exception exception) { }

    public static void Fail() {
        Report(new InvalidOperationException("not started"));
    }
}
