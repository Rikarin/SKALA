using System.Threading;

public sealed class FactAttribute : System.Attribute { }

public sealed class BusTests {
    [Fact]
    public void Delivers() {
        Settle();
    }

    static void Settle() {
        Wait();

        static void Wait() {
            Thread.Sleep(200);
        }
    }
}
