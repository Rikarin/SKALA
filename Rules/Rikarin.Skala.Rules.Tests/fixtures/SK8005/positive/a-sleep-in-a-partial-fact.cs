using System.Threading;

// #400: `[Fact]` on a partial method's definition makes its implementation's body a test, and the
// sleep in it is the one this rule exists for. Read from the implementation alone it was not a test.
public sealed class FactAttribute : System.Attribute { }

public sealed partial class BusTests {
    [Fact]
    public partial void Delivers();

    public partial void Delivers() {
        Thread.Sleep(200);
    }
}
