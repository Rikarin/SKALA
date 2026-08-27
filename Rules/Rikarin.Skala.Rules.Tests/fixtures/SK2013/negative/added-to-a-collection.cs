using System;
using System.Collections.Generic;

public sealed class Collector {
    readonly List<Exception> _failures = new List<Exception>();

    public void Fail() {
        _failures.Add(new InvalidOperationException("not started"));
    }
}
