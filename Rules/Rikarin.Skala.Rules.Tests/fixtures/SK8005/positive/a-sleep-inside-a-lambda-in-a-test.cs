using System;
using System.Threading;

public sealed class TestAttribute : System.Attribute { }

public sealed class PumpTests {
    [Test]
    public void Drains() {
        Action wait = () => Thread.Sleep(50);
        wait();
    }
}
