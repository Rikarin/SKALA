using System.Threading;

public sealed class FactAttribute : System.Attribute { }

public sealed class BusTests {
    public BusTests() {
        Thread.Sleep(200);
    }

    [Fact]
    public void Delivers() { }
}
