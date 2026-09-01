using System;

public sealed class Budget {
    public TimeSpan Remaining(bool exhausted, TimeSpan left) => exhausted ? default(TimeSpan) : left;
}
