using System.Threading;

public sealed class FactAttribute : System.Attribute { }

public sealed class BusTests {
    [Fact]
    public void Delivers() {
        Settle(1);
    }

    public void Settle(int rounds) {
        for (var round = 0; round < rounds; round++) {
            Thread.Sleep(1);
        }
    }
}
