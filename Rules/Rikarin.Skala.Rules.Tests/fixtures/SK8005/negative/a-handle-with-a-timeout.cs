using System;
using System.Threading;

public sealed class FactAttribute : System.Attribute { }

public sealed class BusTests {
    [Fact]
    public void Delivers() {
        using var delivered = new ManualResetEventSlim();
        if (!delivered.Wait(TimeSpan.FromSeconds(10))) {
            throw new InvalidOperationException("nothing was delivered.");
        }
    }
}
