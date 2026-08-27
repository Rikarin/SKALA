using System.Threading;

public sealed class FactAttribute : System.Attribute { }

public sealed class BusTests {
    [Fact]
    public void Delivers() {
        Settle();
    }

    static void Settle() {
        Thread.Sleep(200);
    }
}
