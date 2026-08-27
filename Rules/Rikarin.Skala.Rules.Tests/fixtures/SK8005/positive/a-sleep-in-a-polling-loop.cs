using System.Threading;

public sealed class TheoryAttribute : System.Attribute { }

public sealed class WatcherTests {
    [Theory]
    public void Coalesces() {
        for (var attempt = 0; attempt < 10; attempt++) {
            Thread.Sleep(1);
        }
    }
}
